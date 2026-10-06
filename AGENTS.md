# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## What this is

BrainArena: real-time multiplayer quiz competitions in rooms. Angular (standalone components,
signals) + ASP.NET Core with SignalR + PostgreSQL/EF Core. Built in phases against a fixed product
brief (all four phases — accounts/lobby, room/match flow, question bank + admin, chat — are done);
check `README.md`'s intro section for the current feature list before assuming something exists.

Core non-negotiable rules baked into the design (don't compromise these when touching match code):
the server is the sole authority over timing, correctness and scoring — clients never receive a
correct answer before a question closes, and all scoring uses the server's own clock, never a
client-reported timestamp. The quiz mode itself is pluggable (`IGameMode`); multiple-choice,
calculation, and flash mental arithmetic all exist today (see "The pluggable game mode" and "Flash
Mental Arithmetic" below).

Beyond the original brief's 4 phases, the app also supports **anonymous spectating** (browse the
lobby and watch a live match without an account — see "Spectator mode" below), two more game modes
(**calculation** and **flash mental arithmetic**, alongside multiple-choice), reactions,
**mini-tournaments** (elimination brackets built on top of ordinary rooms — see "Mini-tournaments"
below), **solitary practice rooms** (single-player, auto-starts immediately, playable **without
signing in** — see "Rooms have a Kind" and "Flash Mental Arithmetic" below), a light theme alongside
the dark default, and a single unified create flow for rooms/tournaments/practice — check git
history / this file's other sections before assuming a feature described only in `README.md`'s
phase list is the full picture.

Specialist subagents for this repo live in `.claude/agents/` — `architect`, `frontend-expert`,
`backend-expert`, `game-design-analyst`, `seller-monetization`, `security-expert`, `qa-testing`,
`content-ai`, `growth-analytics`. Each cites concrete facts from this file (stack, rules, what's
built vs. not), so when you change one of those facts, update the agents that repeat it.

## Commands

**Full stack in Docker:** `docker compose up -d --build` → app on http://localhost:8080 (`WEB_PORT`
overrides). `web` is nginx serving the built Angular app and proxying `/api` + `/hubs` (WebSockets) to
`api`, which is deliberately *not* published to the host: compose sets `ForwardedHeaders__Enabled=true`,
and trusting `X-Forwarded-For` (so rate limits see the real client IP, not nginx's) is only safe when
nginx — which overwrites that header — is the sole way in. Keep it off anywhere else.

**Local dev (three terminals):**
```bash
docker compose up -d postgres                           # Postgres only, from repo root
cd backend && dotnet run --project src/BrainArena.Api    # API on :5260, auto-migrates + seeds admin/question bank on start
cd frontend && npm install && npm start                  # Angular dev server on :4200, proxies /api and /hubs to :5260 (frontend/proxy.conf.json) so the app never hardcodes the backend port
```

**Backend** (solution file is `backend/BrainArena.slnx` — the newer XML solution format, not `.sln`):
```bash
dotnet build backend/BrainArena.slnx
dotnet test backend/tests/BrainArena.Application.Tests                 # unit tests, no external deps
dotnet test backend/tests/BrainArena.IntegrationTests                  # needs `docker compose up -d postgres`; full match-flow test takes ~1 min (real per-question timing)
dotnet test --filter "FullyQualifiedName~ScoringServiceTests"          # run a single test class
dotnet ef migrations add <Name> --project backend/src/BrainArena.Infrastructure --startup-project backend/src/BrainArena.Api --output-dir Data/Migrations
```

**Frontend:**
```bash
cd frontend
npm test                 # Vitest (not Karma — see below)
npx ng build
```
No lint script is configured (no ESLint); `prettier` is a devDependency but not wired to an npm script.

## Architecture

### Backend: 4-project layering, dependency direction matters

```
BrainArena.Domain          entities + enums only, zero framework deps
BrainArena.Application     business logic, DTOs, service interfaces — references Domain only
BrainArena.Infrastructure  EF Core, JWT/password hashing, repositories — implements Application interfaces
BrainArena.Api             ASP.NET Core host: controllers, SignalR hub, match orchestrator, Program.cs
```
`Application` never references `Infrastructure` or `Api`. Where `Application`-layer code needs
something only `Api` can provide (e.g. `RoomService` needing to broadcast over SignalR, or trigger
match auto-start), the pattern is: define the interface in `Application/Abstractions/`, implement
it concretely in `Api` (or `Infrastructure`), and register it in `Program.cs`. See `IRoomNotifier`
(implemented by `RoomHub`'s notifier in Api) and `IMatchOrchestrator` (implemented by
`MatchOrchestrator` in Api) for the pattern — this is deliberate, not an oversight, and new
cross-layer needs should follow it rather than having `Application` reference SignalR types directly.

### Match orchestration lives entirely in-memory, in one singleton

`BrainArena.Api/Matches/MatchOrchestrator.cs` is a singleton holding all active matches'
runtime state (`ConcurrentDictionary<RoomId, MatchRuntimeState>`) and drives each one with a
plain async delay loop (`Task.Run` per match — countdown → question → reveal → ... → finished),
not a `System.Threading.Timer`. Match/answer rows are persisted to Postgres as the match
progresses (so a client disconnect never loses data), but a full backend restart mid-match loses
that match's live state — an accepted tradeoff for this app's scale, not a bug to fix reflexively.

A Room has at most one Match in the current design (enforced by a unique index on
`Match.RoomId`) — the frontend routes and the results endpoint are keyed by **room id**, never
match id; the Angular app never needs to learn a match's id at all.

### The pluggable game mode

`BrainArena.Application/Matches/IGameMode.cs` defines the seam: `PrepareQuestionsAsync` (sources
this match's questions — from the admin bank, or generated on the fly), `ToClientPayload` (never
includes the correct answer), `ToRevealPayload`/`ToReviewEntry` (only ever called after a question
closes), `EvaluateAnswer` (takes a mode-agnostic `SubmittedAnswer { OptionIndex, NumericValue }`
plus server-computed `timeRemaining`/`timeLimit`, never trusts the client, and is also where each
mode validates its own answer shape — e.g. an option index actually in range — rather than
`MatchOrchestrator` doing it). A room's mode is just a string column (`Room.GameMode`), validated
against `IGameModeRegistry.ModeKeys` at room-creation time and resolved through the registry at
match start, so a new mode plugs in by registering another `IGameMode` in
`Application/DependencyInjection.cs` — no orchestration code changes.

Three modes exist — the third, `FlashArithmeticGameMode`, extends this seam with just-in-time
round generation and is covered in its own section at the end of this file. The original two:
`MultipleChoiceGameMode` (`Key = "multiple-choice"`, the default, sources
questions from the admin-curated bank via `IQuestionRepository`) and `CalculationGameMode`
(`Key = "calculation"`, procedurally generates arithmetic problems at match start — no admin
authoring, no seed data). Both share one `Question` entity/table, discriminated by
`Question.Type`; `Options`/`CorrectOptionIndex` are multiple-choice-only (nullable),
`CorrectNumericAnswer` is calculation-only (nullable). A `CalculationGameMode` question is a
freshly-constructed, not-yet-persisted `Question` object — `MatchOrchestrator`'s existing
`db.MatchQuestions.AddRange(...)` cascade-inserts it via EF Core's untracked-reachable-entity
behavior the same way it already persists bank-sourced questions, so no branching was needed there.

### One shared SignalR hub, one group per room, reused across a room's whole lifecycle

`RoomHub` (`BrainArena.Api/Hubs/RoomHub.cs`) is the only hub. The Angular `RoomHubService`
(`frontend/src/app/core/services/room-hub.service.ts`) owns a single app-wide connection —
established unconditionally on app bootstrap, *not* per-route and *not* gated on being logged in
(anonymous visitors can spectate), and reconnected on any login/logout transition since a
connection's identity is fixed at handshake time (see the `constructor()` in `app.ts`). Components
join/leave a `room:{roomId}` SignalR group as they navigate, via `JoinRoomGroup`/`LeaveRoomGroup`
(players) or `JoinAsSpectator`/`LeaveSpectatorGroup` (anonymous or non-member visitors — see
"Spectator mode" below). That same group is used for lobby presence in the waiting room *and* live
match events (`QuestionStarted`, `QuestionRevealed`, `MatchEnded`, etc.) — there's no separate
"match group". `JoinRoomGroup` also doubles as the reconnect path: if a match is already active
for that room, it immediately pushes a `MatchResync` payload back to the caller. The waiting-room
component listens for *both* `MatchStarting` and `MatchResync` (plus `MatchSpectatorSync` for
non-participants) as "go to match play" signals — this closes a real race where a player's own
join (filling the room) triggers auto-start server-side before their client has joined the SignalR
group to hear the live broadcast.

Hub methods are REST-adjacent commands (`StartNow`, `SubmitAnswer`, `JoinRoomGroup`,
`LeaveRoomGroup`); room CRUD (`create`, `join`) stays on the REST `RoomsController` — that split
(REST for setup, hub for live gameplay) is intentional.

### Spectator mode: anonymous viewing without a class-level `[Authorize]`

Anyone can browse the public room list and watch a live match without an account — only actually
*playing* (creating/joining/starting/answering) needs one. This meant `RoomHub` could no longer
carry a class-level `[Authorize]` (that gates the SignalR connection handshake itself, before any
method dispatch — an anonymous connection would be rejected outright), so every participant-only
method (`JoinRoomGroup`, `LeaveRoomGroup`, `StartNow`, `SubmitAnswer`, `SendChatMessage`,
`ReportChatMessage`) carries `[Authorize]` individually instead; a reflection test
(`RoomHubAuthorizationTests`) guards against a future method forgetting it. `JoinAsSpectator`/
`LeaveSpectatorGroup` are the anonymous-safe counterparts — no `[Authorize]`, no room-membership
check, and no `RoomConnectionTracker` entry (spectators have no player-domain disconnect side
effect to run; SignalR drops group membership automatically when the connection closes).

The broadcasts a spectator receives are the *same* group-wide `QuestionStarted`/`QuestionRevealed`/
`MatchEnded` events players get — those were already safe (never personalized, never leak an
answer early) and needed no changes. The one payload that did need a non-personalized sibling is
`MatchResync` (carries `YourScore`): `MatchOrchestrator.Snapshot(roomId)` /
`MatchSpectatorSyncPayload` is the spectator equivalent, sent as its own `MatchSpectatorSync` event
so the frontend never has to guess whether a personalized field is meaningful on a given payload.

Private rooms stay fully gated: `RoomService.GetVisibleRoomDetailAsync(roomId, requestingUserId)`
reports a private room as "not found" (same message/404 as a nonexistent room, so a non-member
can't even confirm it exists) unless the caller is an actual member — `RoomsController`'s
`GetOpenRooms`/`GetById`/`GetChatHistory` and `RoomHub.JoinAsSpectator` all route through this
instead of the plain `GetRoomDetailAsync` used by already-membership-checked internal call sites.

### Timing is configurable, not hardcoded, specifically so tests aren't slow

Countdown/reveal durations come from `MatchTimingOptions` (bound from `MatchTiming` in
`appsettings.json`, default 5s/5s to match the product brief exactly). Integration tests override
these to 1s via `IntegrationTestFactory`. The per-question answer window (10-60s) is a *real*
product rule (`RoomValidation`) and is never shortened for tests — a full match integration test
genuinely takes about a minute because of this.

### Integration test database strategy

`BrainArena.IntegrationTests` talks directly to the docker-compose Postgres instance rather than
spinning up its own container (Testcontainers' `.dll` is blocked by this machine's Windows
Application Control policy). Each `IntegrationTestFactory` instance creates a **uniquely-named**
database (`brainarena_test_{guid}`) in `InitializeAsync` and drops it in `DisposeAsync` — a fixed
shared name would race across xUnit's parallel test-class execution. Requires
`docker compose up -d postgres` (or the full stack) to be running. The base factory also relaxes rate limits (every TestServer
request shares one "unknown" client IP) and disables the guest-cleanup background service; a fixture
that needs different settings subclasses it and overrides `ConfigOverrides` (see
`RateLimitedIntegrationTestFactory`). The test project pins `Microsoft.EntityFrameworkCore.Relational`
to the same version Infrastructure compiles against — without it, test code that touches
`BrainArenaDbContext` directly fails with CS1705.

### Chat is one component, reused on every room-lifecycle page, gated on sign-in not on playing

`ChatService`/`ChatRateLimiter`/`ProfanityFilter` all live in `BrainArena.Application.Chat` — note
`ChatRateLimiter` has zero ASP.NET Core dependency (just a `ConcurrentDictionary`) so, unlike
`MatchOrchestrator`, it belongs in `Application` (constructor-testable) rather than `Api`, even
though it's registered as a singleton. The Angular `Chat` component (`features/room/chat/`) is
mounted on `Room`, `MatchPlay`, and `MatchResults` alike, in each page's right-hand sidebar
(`.page-with-sidebar`/`.page-sidebar`, defined once in `styles.scss` and reused by both `Room` and
`MatchPlay`). Its `canSend` input, not its presence, is what gates writing: any registered user can
send at any point in a room's lifecycle — including mid-match, whether they're playing or just
spectating — while an anonymous visitor or a practice-only guest session always gets a read-only
feed (`RoomHub.SendChatMessage` requires the `RegisteredUser` policy, and `ChatService.SendMessageAsync`
enforces private-room membership server-side, keyed only on `room.IsPrivate` + membership, not on
`Room.Status`, so a stale client can't bypass it). This was a deliberate Phase 5→7 product
decision that superseded the original brief's "chat disabled during questions" fairness rule — that
rule was about not giving *players* an unfair signal-passing channel while answering, which chat
send/receive timing doesn't actually affect once anyone (not just room members) can read live
match state anyway. Reported messages are just flagged (`IsReported`) in the DB; there's no
moderation UI yet.

### Competitors panel is a small shared building block, not a scoreboard duplicate

`features/room/competitors-panel/` renders a ranked list of `CompetitorViewModel`s (userId,
displayName, optional score/isConnected/isHost) and is reused by both `Room` (waiting-room player
list, no score yet) and `MatchPlay` (live score, seeded from the room's player list at zero before
any real `ScoreboardEntry` data has arrived, then overwritten by `questionRevealed`/resync events).
`MatchPlay`'s reveal panel no longer renders its own inline scoreboard — the sidebar's
`CompetitorsPanel` is the single always-visible source for standings during a match.

It also owns reactions (Phase 8): a fixed emoji set (`ReactionValidation.AllowedEmojis` on the
backend, mirrored in the frontend's `ALLOWED_REACTIONS` constant — keep both in sync if this set
ever changes) sendable at any competitor by any registered user, same "player or spectator, just
needs a real account" gating as chat. `RoomHub.SendReaction` is ephemeral/never persisted —
purely a `Clients.Group(...).SendAsync("ReactionSent", ...)` broadcast. `CompetitorsPanel`
subscribes to `RoomHubService.reactionSent` itself (it owns the animation, not the parent page) and
renders a transient CSS-only `@keyframes` float-and-fade burst per target avatar — no
`@angular/animations` package, matching the app's zero-dependency convention. Real camera/mic
(WebRTC) is deliberately **not** built — it needs its own architecture decision (mesh vs. SFU) and
was explicitly deferred as a separate future initiative when this phase shipped.

### Mini-tournaments: a `Tournament` sits above `Room`/`Match`, reusing 100% of the match machinery

A `Tournament` (`Domain/Entities/Tournament.cs`) is a top-K-advance elimination bracket: players
join a waiting pool up to `TournamentSize`, and once full (or the creator manually starts it) round
1 splits them into ordinary `Room`s of `RoomSize` players each (`TournamentRoundRoom` links a round
to the real `Room`); each room plays a completely normal match end-to-end through the existing
`MatchOrchestrator`. When a room finishes, the top `AdvancesPerRoom` finishers (capped at
`roomPlayerCount - 1`, so every room eliminates at least one player and the bracket is guaranteed to
converge) carry forward into the next round's rooms; a round is final once the advancing player
count fits in a single room. This reuses Room/Match/`IGameMode`/scoring/SignalR broadcasting
entirely unchanged — a tournament round's room is just a `Room` a player didn't create themselves.

`TournamentService.HandleRoomMatchFinishedAsync` is the round-advancement entry point, called from
`MatchOrchestrator.FinalizeMatchAsync` (resolved via `scope.ServiceProvider.GetRequiredService`,
same captive-dependency-avoidance pattern as the rest of that method — `MatchOrchestrator` is a
Singleton and can't constructor-inject the Scoped `ITournamentService`). Two rooms in the same round
routinely finish within moments of each other, so this is genuinely concurrent; `TournamentAdvancementLock`
(a `ConcurrentDictionary<Guid, SemaphoreSlim>`, one per tournament) serializes advancement, and each
call re-checks `round.RoundNumber == tournament.CurrentRoundNumber` after acquiring the lock in case
a sibling call already advanced the bracket while it was waiting.

**EF Core gotcha that cost real debugging time and is worth knowing before touching this code:**
`HandleRoomMatchFinishedAsync` originally queried the same `Tournament` entity twice on one
`DbContext` — once before acquiring the lock (just to read the tournament ID to lock on) and once
after (the real, mutation-driving fetch). EF Core's change tracker/identity map returns the *same
tracked instance* on a repeat query for an already-tracked entity **without refreshing its
properties from the database** — so the second call's `tournament.CurrentRoundNumber` silently kept
showing the pre-advancement value even after a sibling call had already committed the advance,
defeating the staleness check above and creating two `TournamentRound`s with the same round number.
The lock itself was serializing correctly the whole time; the bug was purely a stale *read*, and no
amount of change-tracking tricks on the *write* side (bulk `ExecuteUpdateAsync`, etc.) could have
fixed it. The fix: `ITournamentRepository.GetTournamentIdForRoomAsync` is a scalar `Guid?`
projection (projections never enter the change tracker) used for the pre-lock check, so the
post-lock fetch is the *only* query touching that `Tournament` entity on the DbContext and is
therefore always a genuinely fresh read. If you add another pre-lock lookup here, make it a
projection too — don't `Include()` the same entity graph twice on one context and expect the second
copy to be current.

Adding a new `TournamentRound`/`TournamentRoundRoom` on an already-tracked, pre-existing `Tournament`
also needs explicit `ITournamentRepository.AddRoundAsync`/`AddRoundRoomAsync` (→ `db.TournamentRounds.AddAsync`/
`db.TournamentRoundRooms.AddAsync`) rather than `tournament.Rounds.Add(...)` navigation-collection
adds — the latter gets tracked as `Modified` instead of `Added` for an entity with an explicit
client-generated key, producing a 0-rows-affected UPDATE instead of an INSERT. Mirrors
`MatchOrchestrator`'s existing `db.MatchQuestions.AddRange(...)` pattern for the same reason.

SignalR-wise, tournaments get their own group (`RoomHub.TournamentGroupName(id)` = `tournament:{id}`,
joined/left via `JoinTournamentGroup`/`LeaveTournamentGroup`) plus a standing `tournament-lobby`
group every connection auto-joins on connect — `TournamentNotifier` sends payload-free
`TournamentListChanged`/`TournamentUpdated` events (same "client refetches GET" pattern as
`RoomUpdated`), never tournament data over the wire. Frontend: tournaments no longer have their own
list page or route (`/tournaments` was removed — see "Unified create flow" below); browsing them is
a tab on `/lobby`, and `features/tournaments/` now holds only `tournament-detail/` — waiting-room
player list + join/leave/start before it starts, a round-by-round bracket view with a "go to your
match" link into the normal `/rooms/:id` page once it has. A tournament room's players are added as
real `RoomPlayer`s at room-creation time, so once a player navigates to their round's room it
behaves exactly like any other room they'd joined directly — `Room`'s existing
participant/spectator/resync logic needs no tournament-specific branching at all.

### Rooms have a Kind: Multiplayer vs. Solitary (practice)

`Room.Kind` (`RoomKind` enum: `Multiplayer`/`Solitary`, EF-converted to a string column like
`Status`/`Topic`/`GameMode`) distinguishes an ordinary room from a single-player practice room. A
solitary room is always exactly 1/1 and never private — `RoomValidation` skips the ordinary
2-player floor entirely for `Kind == Solitary` rather than lowering the shared
`MinPlayersLimit`/`MaxPlayersLimit` constants (those stay untouched because `TournamentValidation`
reuses them directly for `RoomSize` bounds), and `RoomService.CreateRoomAsync` forces
`MaxPlayers`/`MinPlayersToStart` to 1 and `IsPrivate` to `false` server-side regardless of what the
client sent, so a crafted payload can't create a many-player "solitary" room.

A solitary room auto-starts **synchronously inside the create request itself** — since it's already
"full" (1/1) the moment it's created, `CreateRoomAsync` calls the existing
`matchOrchestrator.TryAutoStartAsync` right after saving (the same call `JoinRoomAsync` makes when a
room fills up; `MatchStartRules.CanAutoStart` already just checks `Players.Count >= MaxPlayers`, no
new orchestrator logic needed). **The non-obvious part**: re-checking the room's status afterwards
to build the response DTO can't just call `IRoomRepository.GetByIdAsync` again on the same
`DbContext` — `RoomService` is Scoped (one `DbContext` per request) while `MatchOrchestrator` is a
Singleton that mutates the row via its *own* separately-scoped `DbContext` inside
`TryAutoStartAsync`. A second `GetByIdAsync` call on `RoomService`'s own already-tracking context
hits EF Core's identity map and silently returns the *same stale in-memory instance* instead of
re-reading the database — the exact class of bug already documented in the Mini-tournaments section
above, encountered a second time while building this feature. The fix:
`IRoomRepository.GetStatusNoTrackingAsync` is a scalar `AsNoTracking()` projection (never enters the
change tracker, so it can't hit that trap) used to refresh just the `Status` field on the local
`room` object before mapping the response. If a room's `PrepareQuestionsAsync` can't produce enough
questions for its topic, the match never starts and `CreateRoomAsync`'s response correctly reports
`Status: Waiting` — the frontend treats that as a creation *failure* for a solitary room specifically
(unlike a multiplayer room, nobody can ever join a 1/1 room to retry auto-start), showing an inline
error instead of navigating anywhere.

### Unified create flow: one form, one button, three kinds

`/lobby` is the single entry point for browsing and creating anything playable — it replaced the
old separate `/tournaments` list page (still just `/tournaments/:id` for viewing one bracket).
`Lobby` has three tabs (`activeTab` signal: `'multiplayer' | 'tournament' | 'solitary'`, seedable
from a `?tab=` query param — `tournament-detail`'s back-link uses this to land back on the right
tab) fetching `RoomService.getOpenRooms()` (filtered client-side to `kind === 'Multiplayer'`) and
`TournamentService.getOpenTournaments()` in parallel; the Solitary tab has no browsable list at all
(a solitary room is never private and briefly *does* show up in `GetOpenRoomsAsync`'s
`!IsPrivate && (Waiting || InProgress)` filter while its short match plays, but that's not
meaningfully something to browse) — instead it renders the shared `EmptyState` component as a
"practice now" call-to-action.

One `CreateGameForm` (`features/lobby/create-game-form/`, replacing the old separate
`create-room-form/`/`create-tournament-form/`) handles all three kinds behind a `kind` form control,
pre-selected to whichever lobby tab was active when it opened — applied in `ngOnInit`, because a
signal `input()` read in a field initializer still returns its default (that's how the pre-selection
was silently always "multiplayer" for a while). Without a real account (anonymous or a guest session)
`kinds()` is just `['solitary']`. Field visibility branches on `kind`
in the template; submission routes to `RoomService.create(...)` (multiplayer/solitary) or
`TournamentService.create(...)` (tournament). Names are **always auto-generated**, never typed — no
kind has a manual name input. `CreateGameForm` builds the name from translated game-mode/topic
fragments (`"{{mode}} · {{topic}}"`, topic omitted for calculation mode) with a kind-specific prefix
(`"Practice: "` / `"Tournament: "` / no prefix for multiplayer) — this is a frontend-only concern in
both directions: `CreateTournamentRequest.Name` and `CreateRoomRequest.Name` are still ordinary
required string fields server-side, exactly as before, just never exposed as an editable `<input>`
anymore. Post-submit navigation differs per kind: tournament → `/tournaments/:id`; multiplayer →
share-code panel or `/rooms/:id` (unchanged from before); solitary → checks the *response's*
`status` field and goes straight to `/rooms/:id/play` if it's already `InProgress` (see "Rooms have
a Kind" above) — `MatchPlay.ngOnInit` needs zero changes to handle this, since joining the SignalR
group for a room with an already-active match already triggers the existing `MatchResync` path.

### Header settings dropdown + light/dark theme

The header collapsed its always-visible language-switch/admin-link/logout row into a single
trigger + dropdown on `App` (`app.ts`/`app.html`/`app.scss`): the player chip (avatar + name) for a
signed-in user, or a small gear icon for an anonymous visitor (who gets language + theme only — no
admin/logout). This is the **first outside-click-to-close popover in the codebase** — everything
else that opens/closes (e.g. `CompetitorsPanel`'s reaction picker) only closes via re-click or an
explicit action; `App`'s `@HostListener('document:click')` + injected `ElementRef` containment check
is a new, deliberately simple pattern, not a reuse of an existing one.

`ThemeService` (`core/services/theme.service.ts`) is signal-based, mirroring `AuthService`'s
localStorage pattern (`brainarena.theme` key). Avoiding a flash of the wrong theme on load needed
more than the service itself, though: `App`'s constructor (where a service would normally be
injected eagerly) only runs *after* Angular has bootstrapped and the browser has already painted
once — too late. `index.html` has a tiny synchronous `<script>` in `<head>`, before any
stylesheet/bundle tag, that reads `localStorage`/`prefers-color-scheme` and sets
`data-theme="light"|"dark"` on `<html>` immediately; `ThemeService` then just reads that attribute
back (rather than recomputing storage/`matchMedia` a second time) to seed its own signal, so there's
one source of truth. `styles.scss` defines the light palette as a `:root[data-theme="light"]`
override block for every color token, plus a handful of theme-sensitive non-color values that
can't be plain color tokens (`--header-bg` gradient, `--input-bg`, `--brand-title-from/to` for the
gradient-text logo) — `--color-primary`/`--color-cta`/`--color-gold` stay close to their dark-mode
hues in light mode so brand identity survives the switch; only surfaces, borders, text, and shadow
softness actually change.

### Frontend structure

Standalone components throughout, signals for local state, RxJS `Subject`s for hub events.
`core/` holds cross-cutting services (auth, admin guard, the hub client, theme, i18n loader);
`shared/` holds small reusable presentational components (`LoadingSkeleton`/`EmptyState`/
`ErrorBanner`); `features/` is route-level (`auth/`, `lobby/` — the unified Multiplayer/Tournament/
Solitary browse-and-create page, plus its `create-game-form/`, `admin/` — question bank CRUD +
import, `room/` — waiting room plus `match-play/`, `match-results/`, and `chat/` sub-components, all
keyed by room id, `tournaments/` — now just `tournament-detail/`, keyed by tournament id). i18n via
Transloco, **not** Angular's built-in i18n (needed for runtime language switching without separate
builds) — Spanish is the default/fallback locale, translation files are
`frontend/public/i18n/{es,en}.json`. Test runner is **Vitest** (`@angular/build:unit-test`), not
Karma/Jasmine — this Angular version (22) made Karma a deprecated, webpack-based path.

### Shared UI building blocks + the dark game-lobby visual language

`frontend/src/app/shared/` holds three small, reusable standalone components pulled out during a
UX-polish pass: `LoadingSkeleton` (shimmering placeholder rows — `[rows]`/`[height]` inputs),
`EmptyState` (icon + message panel, `<ng-content>` slot for an optional action), and `ErrorBanner`
(icon + message, optional `[dismissible]`). These replace the old ad hoc `<p class="loading-text">`
/`<p class="empty-state">`/`<p class="error">` scattered per-feature — reach for them first before
writing a new bespoke loading/empty/error treatment.

The whole app runs a dark "arcade lobby" visual theme (`styles.scss` tokens: `--color-bg-deep`,
`--color-surface`/`--color-surface-elevated`, `--color-primary` (violet), `--color-cta` (green,
"go/join" actions), `--color-gold` (create/champion accents), `--font-display` (Rajdhani, headings/
labels/buttons) vs. `--font-body` (Inter, readable content like quiz option text) — loaded via
Google Fonts in `index.html`). Panels consistently use a `linear-gradient(165deg, var(--color-surface-elevated) 0%, var(--color-surface) 100%)`
background + `1px solid var(--color-border)` + `box-shadow: var(--shadow-panel)`; buttons are
glossy/3D by default (global `button` rule in `styles.scss`) with `.secondary` as the flat/outlined
variant. `.status-dot`/`.icon` are global utility classes (glow indicator, inline-SVG icon sizing)
reused across the lobby, tournaments, and room pages rather than redeclared per component — small
inline `<svg>` icons are used throughout instead of emoji or an icon-font dependency, consistent
with the app's zero-dependency convention.

Two small live-data animations worth knowing about if you touch match UI: `match-play`'s countdown
number and the "time low" (`≤5s`) state both use the global `pulse-glow` keyframe; `CompetitorsPanel`
tracks each competitor's previous score in a plain `Map` (not a signal — it's write-only bookkeeping,
never read reactively) inside a constructor `effect()`, and briefly adds a `.bump` class (global
`value-bump` keyframe) whenever a score increases, auto-clearing after 700ms via the same
timer-per-id pattern the reaction-burst code already used. This is a highlight/flash, deliberately
**not** a numeric count-up animation — added scope that wasn't worth the extra complexity for a quiz
score that jumps in coarse increments.

### JSON enum handling gotcha

The Api globally registers a `JsonStringEnumConverter` for MVC responses (`Program.cs`), so
`RoomTopic`/`RoomStatus` etc. serialize as strings over REST. This does **not** carry over to
`System.Net.Http.Json`'s default `HttpClient` extension methods used in integration tests — any
test that round-trips a DTO with an enum property needs its own `JsonSerializerOptions` with that
converter explicitly added (see `RoomJoinFlowTests`/`FullMatchFlowTests` for the pattern). SignalR
hub payloads sidestep this entirely since none of the hub DTOs carry enum properties.

### Flash Mental Arithmetic: a third `IGameMode`, with live-adaptive difficulty in Solitary only

`FlashArithmeticGameMode` (`Key = "flash-arithmetic"`) flashes a sequence of 1-2 digit numbers one
at a time (large white number, black screen between, frontend-only pacing); the player types the
sum once they're all shown. Works in Multiplayer, Tournament, and Solitary rooms like every other
mode — but Solitary's difficulty **adapts live** round to round (rises on a correct answer, drops
on a miss), while Multiplayer/Tournament use a **fixed** difficulty for the whole match, chosen
once at creation, identical for every player — a hard fairness requirement.

**The JIT question-generation engine.** `MatchOrchestrator.StartMatchAsync` used to always call
`IGameMode.PrepareQuestionsAsync` once, building every round upfront — impossible for live-adaptive
difficulty, since round N+1's content depends on whether round N was answered correctly. `IGameMode`
gained two members as **C# default interface methods**, so `MultipleChoiceGameMode`/
`CalculationGameMode` needed zero changes: `RequiresIncrementalGeneration(Room room) => false` and
`PrepareNextQuestionAsync(room, currentAdaptiveState, previousAnswerWasCorrect, ...)`.
`FlashArithmeticGameMode.RequiresIncrementalGeneration` returns true only for `RoomKind.Solitary`;
Multiplayer/Tournament flash-arithmetic rooms still take the ordinary upfront path, generating every
round at the same fixed Level. `previousAnswerWasCorrect == null` signals round 0, telling the mode
to reset to its own baseline Level regardless of whatever placeholder state was passed in.

`MatchRuntimeState` (`Api/Matches/`) grew `Topic`/`Difficulty`/`TargetQuestionCount` (all cached
once from `Room` in `StartMatchAsync`) and a mutable `AdaptiveState` int. `RunMatchLoopAsync`
generates round `i` on demand (`MatchOrchestrator.GenerateNextRoundAsync`, its own DB scope, same
pattern as `FinalizeMatchAsync`) whenever `i >= state.Questions.Count`; for the two upfront modes
that branch never fires, so behavior is unchanged. **Deliberately does not re-fetch `Room`** inside
that per-round loop — Topic/Difficulty are cached on `MatchRuntimeState` instead, avoiding both an
extra DB round-trip per round and a third occurrence of the EF Core stale-read trap already
documented above (Mini-tournaments, "Rooms have a Kind"). A **real pre-existing bug fixed as part of
this refactor**, not just avoided: `RunMatchLoopAsync`/`Join`/`Snapshot` all used
`state.Questions.Count` as a payload's `totalQuestions` — harmless for the two upfront modes
(populated from round 0), but for incremental generation `Questions.Count` starts at 1 and grows, so
a Solitary player would have seen "Question 1 of 1", then "2 of 2", etc. All three call sites now use
the new `state.TargetQuestionCount` instead, which is set once from `room.QuestionCount` and never
changes.

**Level lives on the existing `Question.Difficulty` int**, repurposed as the per-round numeric Level
carrier (previously dead weight for calculation-mode questions) — not to be confused with the new
`Domain.Enums.Difficulty` (`Easy`/`Medium`/`Hard`), a *different* room-level concept: for
Multiplayer/Tournament it's the fixed Level for the whole match, for Solitary it's only the
*starting* Level before the engine takes over. `Room.Difficulty`/`Tournament.Difficulty` mirror how
`Tournament` already duplicates `GameMode`/`Topic`/`QuestionCount` for the same reason
(`TournamentService.CreateNextRoundAsync`'s `new Room {...}` pulls from `tournament.*`).
`QuestionClientPayload` grew a trailing optional `int? Level` populated only by this mode.

**The numbers are streamed server-paced, because the numbers *are* the answer.** Sending the whole
sequence in `QuestionStarted` let a script read it off the WebSocket, sum it instantly, and take the
maximum speed bonus while honest players were still watching the flash. Now
`FlashArithmeticGameMode.ToClientPayload` sends an empty `Text`, and `IGameMode` has a third default
interface method, `GetFlashSequence(MatchQuestion)` (null for every other mode), which returns the
numbers plus display/gap timing (shorter as Level rises, floored at 350 ms). For a non-null sequence,
`RunMatchLoopAsync` runs a `MatchPhase.Flash` sub-phase: `QuestionStarted` (with a projected deadline),
then one `FlashNumber` event per number at its own display moment, then `AnswerWindowOpened` with the
real deadline. `SubmitAnswerAsync` refuses answers during `Flash`, and since `PhaseEndsAtUtc` is only
set to the real deadline when the window opens, the speed bonus is measured from the moment answering
became possible. Resync/spectator sync report phase `"Flash"` mid-flash (the remaining numbers and the
window-open event still arrive live; numbers missed while disconnected are not replayed).

**Anonymous Solitary play**: every Room/Match player row is a NOT NULL FK to a real `Users` row, so
true anonymous play needed a real user to hang off of. `RoomsController.Create` is `[AllowAnonymous]`
using `User.GetUserIdOrNull()`; `RoomService.CreateRoomAsync`'s signature is `Guid? hostUserId` ->
`Task<CreateRoomResult>` (`record CreateRoomResult(RoomDetailDto Room, AuthResponse? GuestAuth)`). A
null `hostUserId` throws 401 unless `request.Kind == RoomKind.Solitary`, in which case `RoomService`
(now also depending on `IPasswordHasher`/`IJwtTokenService`, exactly like `AuthService.BuildResponse`
does) creates a throwaway `User` with `Role = UserRole.Guest` (`guest-{guid}@guest.brainarena.local`)
and mints it a real JWT. The guest can play its own practice rooms (`JoinRoomGroup`, `SubmitAnswer`,
results) and nothing shared — see "Guest sessions are practice-only" below. Frontend:
`AuthService.applyAuth` (was `private`) is now public so `CreateGameForm` can apply `guestAuth` the
same way login/register do; it then **explicitly `await roomHub.disconnect(); await
roomHub.connect();`** before navigating — `MatchPlay.ngOnInit`'s `joinRoomGroup` call is
`[Authorize]`-gated and fires almost immediately after navigation, and `App`'s reactive
reconnect alone gives no guarantee the hub has finished reconnecting under the new identity by then
(the hub service's own single-flight/idempotency guards make the explicit await and that reactive
effect converge safely rather than double-connecting). `App`'s effect keys on `auth.token()`, not on
`isAuthenticated()`: a guest who then registers or logs in stays "authenticated" the whole time, and
a flip-based check would leave the hub running as the guest.

**Frontend panel** (`features/room/match-play/flash-arithmetic-panel/`) mirrors
`calculation-panel.ts`'s patterns (effect-driven reset on a new `question()` input by identity,
`FormControl.disable()`/`.enable()` rather than a template `[disabled]` binding). It's a pure view of
server events: `MatchPlay` forwards the latest `FlashNumber` (filtered to the current question) and an
`answerWindowOpen` flag as inputs; the panel shows each number for its full `visibleMs` from when it
arrives, and only offers the answer box once the window is open *and* no number is still on screen
(`stage` is a `computed`: `'flash' | 'gap' | 'input'`). That ordering matters: on a lagging or
throttled device the last `FlashNumber` and `AnswerWindowOpened` can land in the same zoneless render,
and blanking on window-open made the last number never paint (seen in a real browser run).
`MatchPlay.submitNumeric` also refuses to send before the window opens.

**Numeric answer forms need `[formGroup]`.** `(ngSubmit)` is an output of `FormGroupDirective`/`NgForm`
only; on a bare `<form>` in a `ReactiveFormsModule`-only component it never fires, and the button's
native submit reloads the whole page (URL gains a trailing `?`) without sending the answer. Both
`CalculationPanel` and `FlashArithmeticPanel` had this — calculation answers never reached the server
from the real UI — and now wrap their control in an `answerForm` `FormGroup`. Their specs dispatch a
real `submit` event and assert `defaultPrevented`, since calling `submit()` directly can't catch it. The flash stage's
`#000`/`#fff` colors are **hardcoded, not
theme tokens** — deliberately opting out of the light/dark theme system since the mechanic depends on
stark black/white contrast for fast number recognition. **Level/Streak/BestStreak/Accuracy HUD state
lives in `MatchPlay`, not the panel** — `match-play.html` mounts the question-panel and reveal-panel
in separate top-level `@if` blocks, and Angular's `@if` is structural (destroys/recreates on
transition), so a fresh panel instance is created on every single reveal; owning the HUD state there
would have silently zeroed it every round. `MatchPlay` tallies streak/accuracy itself in its
`questionRevealed` subscription, comparing its own `mySubmittedAnswer` signal against the revealed
correct answer (both already client-side; no new backend field needed). **`match-play.html`'s shared
`<h2>{{ q.text }}</h2>`** (rendered above every mode's panel) is guarded with
`@if (q.kind !== 'flash-arithmetic')` in both the question and reveal blocks (belt and braces now
that the server sends an empty `Text` for this mode anyway).

### Final standings are a strict order, with a fixed tie-break

`MatchRanking.Order` (`Application/Matches/`) is the single ordering used by both the live scoreboard
(`MatchRuntimeState.BuildScoreboard`) and the persisted `FinalRank` (`FinalizeMatchAsync`): score,
then more correct answers, then less total time spent on correct answers (`PlayerAnswerRuntime.TimeTaken`,
measured from the answer window opening), then room join order (`PlayerRuntime.JoinOrder`, from
`RoomPlayer.JoinedAt` then user id — in a tournament round room that's its bracket seeding). It never
produces shared ranks, because tournament advancement takes the top N by position; before this, tied
players got distinct ranks in arbitrary dictionary order, which silently decided who advanced.

### Guest sessions are practice-only; abuse limits and cleanup

**`UserRole.Guest`** marks the throwaway accounts minted for anonymous Solitary practice. The
`AuthPolicies.RegisteredUser` policy (role Player or Admin) guards everything shared with real
players: `RoomsController.Join`/`GetByCode`, all of `TournamentsController` (reads stay
`[AllowAnonymous]`), and the hub's `StartNow`/`SendChatMessage`/`ReportChatMessage`/`SendReaction`
(asserted by `RoomHubAuthorizationTests`). `RoomService.CreateRoomAsync` itself returns 403 when a
guest asks for a non-Solitary room — that endpoint has to stay `[AllowAnonymous]` for the guest-minting
path, so it can't be expressed as a policy. Frontend mirrors this with `AuthService.isGuest()` /
`isRegistered()`: shared features gate on `isRegistered()`, `CreateGameForm` offers only Solitary,
and the header menu offers guests "Create account". Keeping guests contained is also what makes
cleanup safe — the user's own requirement was "Solitary without sign-in, multiplayer needs an account".

**Cleanup**: `GuestCleanupService` (Api, hosted `BackgroundService`, hourly via `GuestCleanup` config)
calls `IGuestCleanup` (Infrastructure `GuestCleanup`) to delete guests created more than
`Jwt:ExpiryDays + 1` days ago — a guest has no password, so once its only token expires nobody can
reach its data again. In one transaction it deletes the guest's rooms (Postgres cascades take
`RoomPlayers`, `ChatMessages`, `Matches` → `MatchQuestions`/`MatchPlayers` → `MatchAnswers`), then the
calculation/flash `Questions` those matches generated (collected first, since `MatchQuestion → Question`
is `Restrict`), then the users. Guests entangled with real players' history (a seat in someone else's
room, a hosted room with other players, any tournament) are skipped, never rewritten. Migration
`MarkExistingGuestUsers` re-tags guests created before the role existed (they were stored as `Player`).

**Rate limiting** (`Api/RateLimiting/`, ASP.NET Core's built-in limiter, `app.UseRateLimiter()` after
authentication): token buckets configured under `RateLimiting` in `appsettings.json` — `auth` policy
(login + register, per client IP), `room-creation` policy (per user id when signed in; anonymous
creation — which mints a guest — per IP, with a burst sized for a classroom behind one NAT).
Rejections return a 429 `ProblemDetails` with a `title`, the same shape `ExceptionHandlingMiddleware`
uses, so existing `err.error.title` handling in Angular just works. Limits run before validation, so a
rejected-anyway request still spends a token. Partitions use `RemoteIpAddress` — behind a reverse
proxy that needs forwarded-headers configuration first. Hub invocations (e.g. reactions) are not
rate-limited.
