# BrainArena

Real-time multiplayer quiz competitions in rooms. Angular + ASP.NET Core (SignalR) + PostgreSQL.

Built in phases per the project brief, then extended well past it — the original four phases are
done, and the app has grown two more game modes, spectating, tournaments, solo practice (no
account needed), and a full visual/theming redesign on top:

- **Phase 1 — accounts and lobby**: register/login (JWT), a lobby that lists open rooms live
  (via SignalR) with a "Create room" form.
- **Phase 2 — room and match flow**: a live waiting room (host badge, "Start now", host migration
  if the host leaves), auto-start when the room fills, a 5-second countdown, questions one at a
  time with server-owned timing and scoring (100 pts + up to 50 speed bonus), a 5-second reveal
  with a live scoreboard after each question, disconnect/reconnect support mid-match, and a final
  results screen with a per-question review.
- **Phase 3 — question bank**: 20 original questions per topic (80 total). An admin-only page
  (`/admin`, gated by role) to add and edit questions and bulk-import them from a JSON file.
- **Phase 4 — chat**: live text chat, available on every room-lifecycle page (waiting room, match
  play, and results) to any signed-in user — playing or just spectating. Server-enforced:
  200-character cap, 1 message per 2 seconds per user, a basic profanity filter (censors blocked
  words rather than rejecting the message), and a Report button on every message.
- **Phase 5 — spectator mode**: anyone can browse the public room list and watch a live match in
  real time (live questions, reveals, scores) without an account — only creating, joining, or
  playing in a room needs one. Private rooms stay fully gated either way.
- **Phase 6 — calculation mode**: a second, pluggable game mode alongside multiple-choice —
  procedurally generated arithmetic problems (no admin authoring needed), scored on the same
  100-points-plus-speed-bonus curve.
- **Phase 7 — right-rail layout**: a persistent competitors panel (live scores, connection status)
  and chat sit side by side with the match itself, on a shared two-column layout used by the
  waiting room and match play alike.
- **Phase 8 — reactions**: a small emoji set sendable at any competitor from the competitors panel,
  with a lightweight floating animation. (Real camera/mic video chat was scoped out as a separate
  future initiative — see Known limitations.)
- **Phase 9 — mini-tournaments**: elimination brackets built on top of ordinary rooms — join a
  tournament's waiting pool, and once it's full each round splits players into rooms that play a
  completely normal match; the top finishers from each room advance until a single champion remains.
- **Phase 10 — visual redesign**: a dark, premium "game lobby" look (gradient panels, glowing status
  indicators, glossy buttons, condensed display type) replacing the original plain UI, plus shared
  loading/empty/error-state components used consistently across the app.
- **Phase 11 — unified play flow**: one "Create room" form covers three kinds of game — regular
  multiplayer rooms, mini-tournaments, and solitary practice rooms (single-player, starts
  instantly, no waiting room) — and everything browsable (rooms and tournaments) lives on one
  tabbed lobby page. Room/tournament names are auto-generated from the game mode and topic instead
  of typed. The header collapsed into a single settings dropdown (language, light/dark theme,
  admin link, logout), and the app now supports a light theme alongside the dark default.
- **Phase 12 — flash mental arithmetic**: a third game mode — numbers flash on screen one at a
  time and you type their sum. Works in multiplayer rooms, tournaments, and solo practice, with an
  Easy/Medium/Hard difficulty picked at creation. In multiplayer and tournaments the difficulty is
  fixed and every player sees the same numbers; in solo practice the level adapts live (up after a
  correct answer, down after a miss), with a level/streak/best-streak/accuracy HUD. Solo practice
  no longer needs an account — starting one anonymously creates a throwaway guest session behind
  the scenes.
- **Phase 13 — fair play and abuse hardening**: flash-arithmetic numbers are streamed by the server
  one at a time, so the full sequence never reaches the browser early, and answering (with its
  speed bonus) only opens after the last number. Ties in the final standings follow a fixed rule
  (more correct answers, then faster correct answers, then join order) instead of falling out
  arbitrarily — this decides who advances in a tournament. Login, registration and room creation
  are rate-limited. Guest sessions are practice-only (multiplayer, tournaments, chat and reactions
  need a real account) and are deleted automatically once they expire.

## Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) or later
- [Node.js 20+](https://nodejs.org/) and npm
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Postgres via
  `docker compose up -d postgres` — the integration tests also need it running, since they create a
  disposable database on that same Postgres instance)

## Running everything with Docker

One command builds and starts the database, the API and the frontend together:

```bash
docker compose up -d --build
```

Then open `http://localhost:8080` (set `WEB_PORT` to use another port, e.g.
`WEB_PORT=4300 docker compose up -d --build`). nginx serves the Angular app and forwards `/api`
and `/hubs` (including SignalR WebSockets) to the API container, which migrates and seeds the
database on startup. The API itself isn't exposed on the host. Follow logs with
`docker compose logs -f api`, and stop everything with `docker compose down` (add `-v` to also
wipe the database volume). This uses the same Postgres container and data volume as the local-dev
setup below.

## Running locally (for development)

Three pieces, three terminals — fastest for day-to-day work since both backend and frontend
reload on change:

**1. Database**

```bash
docker compose up -d postgres
```

Starts Postgres on `localhost:5432` (db `brainarena`, user `brainarena` — see
`docker-compose.yml`). Data persists in a named Docker volume between runs.

**2. Backend API**

```bash
cd backend
dotnet run --project src/BrainArena.Api
```

Runs on `http://localhost:5260` by default. On startup it automatically applies EF Core
migrations, seeds an admin account from config, and seeds the question bank if empty (see
`backend/src/BrainArena.Api/appsettings.json` — `Admin:Email` / `Admin:Password`, defaults to
`admin@brainarena.local` / `ChangeMe123!` for local dev). Log in with that account to reach the
admin question-bank page at `/admin` (also linked from the header once logged in as an admin).
**Change the `Jwt:SigningKey` and `Admin:Password` values before deploying anywhere real.**

**3. Frontend**

```bash
cd frontend
npm install
npm start
```

Runs on `http://localhost:4200`. Requests to `/api/*` and the `/hubs/*` SignalR endpoint are
proxied to the backend (see `frontend/proxy.conf.json`), so the Angular app never needs to know
the backend's port directly.

Open `http://localhost:4200`, register an account, and you should land on the lobby. Open a
second browser (or a private window) with a second account to try a full match with `docker
compose up -d` + the backend + the frontend all running.

## Project structure

```
backend/
  src/
    BrainArena.Domain/         entities + enums, no framework dependencies
    BrainArena.Application/    game/room/match logic, DTOs, service interfaces (unit-testable in isolation)
    BrainArena.Infrastructure/ EF Core, JWT/password hashing, EF-backed repositories, question seed data (20/topic)
    BrainArena.Api/            ASP.NET Core host: controllers, the SignalR hub, the match orchestrator, Program.cs
  tests/
    BrainArena.Application.Tests/  unit tests (scoring, validation, room/match-start rules, chat rate
                                    limiting/profanity filter — no DB needed)
    BrainArena.IntegrationTests/   full-stack tests against a real Postgres database, including a
                                    3-player join-to-finish match and a live chat flow, both over
                                    real HTTP + SignalR
frontend/
  src/app/
    core/        auth/admin guards, interceptor, HTTP services, the SignalR hub client, theme
                 service, i18n loader
    shared/      small reusable presentational components (loading skeleton, empty state, error
                 banner)
    features/    auth (login/register), lobby (unified rooms/tournaments/practice browse + create),
                 room (waiting room + chat + competitors panel, match play, results), tournaments
                 (bracket detail view), admin (question bank)
  public/i18n/   es.json (default) and en.json translation files
docker-compose.yml   full stack (postgres + api + web/nginx); `up -d postgres` for the database alone
backend/Dockerfile, frontend/Dockerfile, frontend/nginx.conf   container images for the full stack
.claude/agents/      specialist Claude Code subagents (architect, frontend, backend, game design,
                     monetization, security, QA, AI content, analytics)
```

## Tests

```bash
# Backend unit tests (no external dependencies)
cd backend
dotnet test tests/BrainArena.Application.Tests

# Backend integration tests (needs `docker compose up -d postgres` running — uses disposable databases
# on that Postgres instance; the full match-flow test takes about a minute since it plays through
# real per-question timing)
dotnet test tests/BrainArena.IntegrationTests

# Frontend unit tests
cd frontend
npm test
```

## Internationalization

The UI defaults to Spanish with an English toggle inside the header's settings dropdown, powered by
[Transloco](https://jsverse.github.io/transloco/). Translation files live in `frontend/public/i18n/`.

## Theming

Dark is the default look; a light theme is available from the same header dropdown as the language
toggle. On first visit the app follows the browser/OS's `prefers-color-scheme` — a manual choice
afterward is remembered (`localStorage`) and takes over from then on.

## Known limitations

- Match state (current question, scores-in-progress, etc.) lives in server memory while a match is
  running, for a simple, fast implementation. Match/answer rows are persisted as the match
  progresses, so a client disconnect never loses data — but a full backend restart mid-match would
  lose that match's live state.
- The 5-second countdown and 5-second reveal durations are configurable (`MatchTiming` in
  `appsettings.json`) — the integration tests shorten them to keep the suite fast, while the
  per-question answer window (10-60s, a real product rule) is left untouched.
- If a room's question count exceeds what's available for its topic (multiple-choice mode only —
  now unlikely at 20/topic, but possible after heavy play or a narrow admin edit; calculation mode
  generates its own problems and never hits this), the match fails to start with a clear error
  rather than starting broken. For a solitary practice room specifically, since nobody else can
  ever join a single-player room to trigger a retry, this surfaces as a creation failure in the
  create form itself rather than a stuck waiting room.
- Real camera/mic video chat was scoped out as a separate future initiative — it needs its own
  architecture decision (peer-to-peer mesh vs. a dedicated media server) before it can be built.
  Reactions (Phase 8) ship today; live video/audio doesn't yet.
- Solitary practice rooms are just you against the clock — there's no history of past solo
  sessions to browse back through yet, only the option to start a new one.
- The question bank's `Language` field (es/en) is captured per question but matches don't yet
  filter by it — question selection is topic-only. Wiring room/match language selection to it is
  natural follow-up work, not done here since the brief didn't call for a room-level language
  picker in any phase so far.
- The admin page supports add, edit, and JSON import (matching the brief); there's no delete
  endpoint since the brief didn't ask for one.
- Rate limits are keyed on the client's IP address. Behind a reverse proxy or load balancer, the
  app needs forwarded-headers configuration first, or every user would share the proxy's limit.
  The limits themselves are configurable under `RateLimiting` in `appsettings.json`.
- Reported chat messages are just flagged (`IsReported = true`) in the database — there's no
  admin moderation view to act on reports yet, since the brief only asked for the Report button
  itself, not a review workflow.
