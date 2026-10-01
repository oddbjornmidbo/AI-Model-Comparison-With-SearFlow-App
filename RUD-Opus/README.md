# SeatFlow

Workshop registrations for a small conference: capacity, FIFO waitlists, automatic promotion, schedule-conflict
protection, idempotent registration and concurrency-safe seat allocation.

```
backend/   .NET 9 minimal API + EF Core (SQLite) + xUnit tests
frontend/  React 19 + TypeScript + Vite, Storybook 10
```

## Running

Prerequisites: .NET 9 SDK, Node 20+.

### Backend (http://localhost:5080)

```sh
cd backend
dotnet run --project SeatFlow.Api        # creates and seeds seatflow.db on first start
dotnet test                              # run the test suite
```

Delete `backend/SeatFlow.Api/seatflow.db*` to reset to seed data. `backend/global.json` pins SDK 9.0.x.
(On this machine the .NET 9 SDK lives in `~/.dotnet`; use `~/.dotnet/dotnet` if `dotnet` resolves to an older SDK.)

### Frontend (http://localhost:5173)

```sh
cd frontend
npm install
npm run dev              # proxies /api to http://localhost:5080, so start the backend first
npm run build            # type-check + production build
npm run lint
npm run storybook        # component stories on http://localhost:6006 (no backend needed)
```

## API

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/api/workshops` | With confirmed/waitlisted counts and remaining seats |
| GET | `/api/workshops/{id}` | |
| POST | `/api/workshops` | `{title, startTime, endTime, capacity}` → 201, 400 on validation errors |
| GET | `/api/workshops/{id}/registrations` | All statuses in FIFO order, with `waitlistPosition` |
| POST | `/api/workshops/{id}/registrations` | `{participantId}`, optional `Idempotency-Key` header → 201 Confirmed/Waitlisted |
| GET | `/api/registrations/{id}` | |
| POST | `/api/registrations/{id}/cancel` | 200, idempotent |
| GET | `/api/participants` | |
| POST | `/api/participants` | `{name, email}` (used by tests; no UI) |
| GET | `/api/participants/{id}/schedule` | Participant's registrations ordered by workshop start |
| GET | `/api/notifications` | Dev aid: notifications recorded by the in-memory service |

Errors are RFC 7807 problem details with a machine-readable `code`:
`404 not_found`, `409 duplicate_registration`, `409 schedule_conflict`, `422 idempotency_key_reused`,
`400` validation problems with per-field `errors`.

## Architecture

- **`Domain/RegistrationService`** holds all registration rules (register, cancel, waitlist promotion). Endpoints only
  validate input, call the service and map to DTOs. There is no repository layer; the service uses the `DbContext`
  directly.
- **Transactions and concurrency.** Each command runs in a SQLite `BEGIN IMMEDIATE` transaction, which takes the
  database write lock *before* the first read. Capacity checks, overlap checks, duplicate checks and idempotency lookups
  are therefore serialized, so two requests can never both see the last free seat. This works across threads and
  processes. Waiting writers are retried by Microsoft.Data.Sqlite up to `Default Timeout` (30 s). WAL mode lets reads
  continue while a write is in progress. A filtered unique index (one non-cancelled registration per participant and
  workshop) backs up the duplicate rule at the database level.
- **Waitlist promotion.** When a confirmed seat is cancelled, the waitlist is walked oldest first (`CreatedAt`, then
  `Id`). Participants who would get overlapping confirmed workshops are skipped and stay on the waitlist; the first
  eligible one is promoted. Each promotion is saved immediately, so later conflict checks in the same transaction see
  it. Cancelling a confirmed seat also re-runs promotion for the other workshops where that participant is waitlisted,
  because their conflict may just have disappeared.
- **Idempotency.** An `IdempotencyRecord` (key → workshop, participant, registration) is written in the same
  transaction as the registration. A repeated key returns the same registration with `201` and
  `Idempotent-Replayed: true`. Reusing a key for a different workshop or participant returns `422`.
- **Notifications.** `INotificationService` is called only *after* commit, for: confirmed on registration, promoted from
  waitlist, and cancelled. Exceptions are caught and logged, so a failure never undoes the committed change. The dev
  implementation `InMemoryNotificationService` logs and keeps the last 500 notifications.
- **Frontend.** `App.tsx` is the only stateful container (data fetching through `api.ts` and `useAsync`). All other
  components are presentational and each has Storybook stories covering its states (loading, empty, error, populated,
  submitting, every registration status). Each register submit sends a fresh `crypto.randomUUID()` as the
  `Idempotency-Key`. After a register or cancel, the UI refetches workshops, the open registration list and the open
  schedule, so automatic promotions are visible straight away.

### Tests

`backend/SeatFlow.Tests` runs the real HTTP pipeline (`WebApplicationFactory`) against a temporary SQLite file per test,
with a recording or failing notification service. It covers: confirm when seats are free, waitlist when full (with
positions), FIFO promotion, skipping a conflicting waitlisted participant, a seat freed by a conflict going to that
participant later, overlap prevention (back-to-back sessions are allowed), duplicate prevention and re-registration
after cancel, idempotent replay (sequential and concurrent), key reuse with a different payload, cancelling twice,
20 concurrent requests for the final seat, concurrent registration for two overlapping workshops, notification failures
not rolling back, 404s and validation errors.

## Assumptions

- **Overlap.** Time ranges are half-open: a session ending at 10:00 does not overlap one starting at 10:00.
- **Conflicts on registration.** If seats are free but the participant is confirmed for an overlapping workshop, the
  registration is rejected with `409 schedule_conflict`. If the workshop is full, they are waitlisted anyway; the
  conflict is checked again at promotion time, when they may have cancelled the other workshop.
- **Seats left free by conflicts.** When every waitlisted participant has a conflict, the freed seat stays free. A new
  registrant may then take it, even though older (ineligible) waitlist entries exist.
- **Cancelling a waitlisted registration** removes it from the queue and triggers no promotion in that workshop.
- **Idempotency scope.** Keys are global, are stored only for successful registrations (a failed attempt may be retried
  with the same key) and never expire. A replay returns the registration's *current* state, which may have changed
  since the first request (e.g. it was promoted or cancelled).
- **Notifications.** Being waitlisted is not one of the specified triggers, so no notification is sent for it. A
  repeated cancel sends nothing.
- **Times** are stored and returned in UTC. The UI shows them in the viewer's local time zone, in English.
- **Seed data** is a single conference day (12 Nov 2026) with 5 workshops and 6 participants. Two pairs of workshops
  partially overlap: Event Sourcing/DDD and Concurrency/Kubernetes. DDD is full, and its first waitlisted participant
  (Alan) has a conflict, so cancelling a DDD seat shows the skip-and-promote rule.

## Known limitations

- The schema is created with `EnsureCreated`; there are no EF migrations.
- SQLite's single-writer lock makes the concurrency guarantee simple but serializes all registration writes. On a
  server database, a per-workshop row lock (`SELECT … FOR UPDATE`) or serializable transactions would be needed instead.
- Notifications are sent in-process after commit, with no outbox. A crash between commit and send loses the
  notification, and failures are not retried.
- The UI has no forms for creating workshops or participants; the API supports both.
- Storybook stories are for visual inspection and do not run as automated tests (no Vitest/Storybook test runner).
- No authentication, by design.
