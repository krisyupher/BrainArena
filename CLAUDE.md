# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

BrainArena: real-time multiplayer quiz competitions in rooms. Angular (standalone components,
signals) + ASP.NET Core with SignalR + PostgreSQL/EF Core. Built in phases against a fixed product
brief (all four phases — accounts/lobby, room/match flow, question bank + admin, chat — are done);
check `README.md`'s intro section for the current feature list before assuming something exists.

Core non-negotiable rules baked into the design (don't compromise these when touching match code):
the server is the sole authority over timing, correctness and scoring — clients never receive a
correct answer before a question closes, and all scoring uses the server's own clock, never a
client-reported timestamp. The quiz mode itself is pluggable (`IGameMode`) even though only
multiple-choice exists today.

## Commands

**Local dev (three terminals):**
```bash
docker compose up -d                                    # Postgres only, from repo root
cd backend && dotnet run --project src/BrainArena.Api    # API on :5260, auto-migrates + seeds on start
cd frontend && npm install && npm start                  # Angular dev server on :4200, proxies /api and /hubs to :5260
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

`BrainArena.Application/Matches/IGameMode.cs` defines the seam: `ToClientPayload` (never includes
the correct answer), `ToRevealPayload` (only called after a question closes), `EvaluateAnswer`
(takes server-computed `timeRemaining`/`timeLimit`, never trusts the client). Only
`MultipleChoiceGameMode` exists; a room's mode is just a string column (`Room.GameMode`) resolved
through `IGameModeRegistry`, so a second mode plugs in without touching orchestration code.

### One shared SignalR hub, one group per room, reused across a room's whole lifecycle

`RoomHub` (`BrainArena.Api/Hubs/RoomHub.cs`) is the only hub. The Angular `RoomHubService`
(`frontend/src/app/core/services/room-hub.service.ts`) owns a single app-wide connection —
connected once the user is authenticated (see the `effect()` in `app.ts`), *not* per-route — and
components join/leave a `room:{roomId}` SignalR group as they navigate, via `JoinRoomGroup`/
`LeaveRoomGroup`. That same group is used for lobby presence in the waiting room *and* live match
events (`QuestionStarted`, `QuestionRevealed`, `MatchEnded`, etc.) — there's no separate
"match group". `JoinRoomGroup` also doubles as the reconnect path: if a match is already active
for that room, it immediately pushes a `MatchResync` payload back to the caller. The waiting-room
component listens for *both* `MatchStarting` and `MatchResync` as "go to match play" signals —
this closes a real race where a player's own join (filling the room) triggers auto-start
server-side before their client has joined the SignalR group to hear the live broadcast.

Hub methods are REST-adjacent commands (`StartNow`, `SubmitAnswer`, `JoinRoomGroup`,
`LeaveRoomGroup`); room CRUD (`create`, `join`) stays on the REST `RoomsController` — that split
(REST for setup, hub for live gameplay) is intentional.

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

### Chat is one component, reused, and only mounted on two pages

`ChatService`/`ChatRateLimiter`/`ProfanityFilter` all live in `BrainArena.Application.Chat` — note
`ChatRateLimiter` has zero ASP.NET Core dependency (just a `ConcurrentDictionary`) so, unlike
`MatchOrchestrator`, it belongs in `Application` (constructor-testable) rather than `Api`, even
though it's registered as a singleton. "Disabled during questions" is enforced two ways: the
Angular `Chat` component (`features/room/chat/`) is only ever placed in the `Room` (waiting room)
and `MatchResults` templates, never `MatchPlay` — and `ChatService.SendMessageAsync` independently
rejects sends server-side while `Room.Status == InProgress`, so a stale client can't bypass it.
Reported messages are just flagged (`IsReported`) in the DB; there's no moderation UI yet.

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
