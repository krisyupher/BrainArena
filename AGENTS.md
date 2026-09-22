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
client-reported timestamp. The quiz mode itself is pluggable (`IGameMode`); multiple-choice and
calculation both exist today.

Beyond the original brief's 4 phases, the app also supports **anonymous spectating** (browse the
lobby and watch a live match without an account — see "Spectator mode" below) and a **second game
mode** (calculation, alongside multiple-choice) — check git history / this file's other sections
before assuming a feature described only in `README.md`'s phase list is the full picture.

## Commands

**Local dev (three terminals):**
```bash
docker compose up -d                                    # Postgres only, from repo root
cd backend && dotnet run --project src/BrainArena.Api    # API on :5260, auto-migrates + seeds admin/question bank on start
cd frontend && npm install && npm start                  # Angular dev server on :4200, proxies /api and /hubs to :5260 (frontend/proxy.conf.json) so the app never hardcodes the backend port
```

**Backend** (solution file is `backend/BrainArena.slnx` — the newer XML solution format, not `.sln`):
```bash
dotnet build backend/BrainArena.slnx
dotnet test backend/tests/BrainArena.Application.Tests                 # unit tests, no external deps
dotnet test backend/tests/BrainArena.IntegrationTests                  # needs `docker compose up -d`; full match-flow test takes ~1 min (real per-question timing)
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

Two modes exist: `MultipleChoiceGameMode` (`Key = "multiple-choice"`, the default, sources
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
`docker compose up -d` to be running.

### Chat is one component, reused on every room-lifecycle page, gated on sign-in not on playing

`ChatService`/`ChatRateLimiter`/`ProfanityFilter` all live in `BrainArena.Application.Chat` — note
`ChatRateLimiter` has zero ASP.NET Core dependency (just a `ConcurrentDictionary`) so, unlike
`MatchOrchestrator`, it belongs in `Application` (constructor-testable) rather than `Api`, even
though it's registered as a singleton. The Angular `Chat` component (`features/room/chat/`) is
mounted on `Room`, `MatchPlay`, and `MatchResults` alike, in each page's right-hand sidebar
(`.page-with-sidebar`/`.page-sidebar`, defined once in `styles.scss` and reused by both `Room` and
`MatchPlay`). Its `canSend` input, not its presence, is what gates writing: any signed-in user can
send at any point in a room's lifecycle — including mid-match, whether they're playing or just
spectating — while an anonymous guest always gets a read-only feed (`ChatService.SendMessageAsync`
enforces the same rule server-side, keyed only on `room.IsPrivate` + membership for private rooms,
not on `Room.Status`, so a stale client can't bypass it). This was a deliberate Phase 5→7 product
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

### Frontend structure

Standalone components throughout, signals for local state, RxJS `Subject`s for hub events.
`core/` holds cross-cutting services (auth, admin guard, the hub client, i18n loader); `features/`
is route-level (`auth/`, `lobby/`, `admin/` — question bank CRUD + import, `room/` — waiting room
plus `match-play/`, `match-results/`, and `chat/` sub-components, all keyed by room id). i18n via
Transloco, **not** Angular's built-in i18n (needed for runtime language switching without separate
builds) — Spanish is the default/fallback locale, translation files are
`frontend/public/i18n/{es,en}.json`. Test runner is **Vitest** (`@angular/build:unit-test`), not
Karma/Jasmine — this Angular version (22) made Karma a deprecated, webpack-based path.

### JSON enum handling gotcha

The Api globally registers a `JsonStringEnumConverter` for MVC responses (`Program.cs`), so
`RoomTopic`/`RoomStatus` etc. serialize as strings over REST. This does **not** carry over to
`System.Net.Http.Json`'s default `HttpClient` extension methods used in integration tests — any
test that round-trips a DTO with an enum property needs its own `JsonSerializerOptions` with that
converter explicitly added (see `RoomJoinFlowTests`/`FullMatchFlowTests` for the pattern). SignalR
hub payloads sidestep this entirely since none of the hub DTOs carry enum properties.
