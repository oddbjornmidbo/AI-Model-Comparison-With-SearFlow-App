# SeatFlow

Workshop registrations for a small conference: capacity, FIFO waitlists, automatic promotion, schedule-conflict checks, idempotent registration and concurrency-safe seat allocation.

```
backend/   .NET 9 Web API (minimal APIs) + EF Core + SQLite, xUnit tests
frontend/  React + TypeScript + Vite
```

## Running

**Prerequisites:** .NET 9 SDK and Node 20+ (tested with Node 22).

### Backend

```bash
cd backend
dotnet run --project src/SeatFlow.Api --launch-profile http   # http://localhost:5080
dotnet test                                                   # run the test suite
```

The SQLite file `seatflow.db` is created and seeded the first time the API starts, in the directory you run it from. Delete the file to reset the data.

### Frontend

```bash
cd frontend
npm install
npm run dev        # http://localhost:5173 (sends /api to :5080)
npm run build      # type-check + production build
```

Start the backend first, then open http://localhost:5173.

## API

| Method | Path | Notes |
|---|---|---|
| GET  | `/api/workshops` | Includes confirmed/waitlist counts and remaining capacity |
| POST | `/api/workshops` | `{title, startTime, endTime, capacity}` → 201, or 400 validation problem |
| GET  | `/api/workshops/{id}/registrations` | All registrations in FIFO order, with `waitlistPosition` |
| POST | `/api/workshops/{id}/registrations` | `{participantId}` plus an optional `Idempotency-Key` header → 201 (Confirmed or Waitlisted) |
| GET  | `/api/registrations/{id}` | |
| POST | `/api/registrations/{id}/cancel` | 200 `{registration, alreadyCancelled, promoted[]}` |
| GET  | `/api/participants` | Used by the UI |
| GET  | `/api/participants/{id}/schedule` | All of the participant's registrations, ordered by start time |
| GET  | `/api/notifications` | Notifications recorded by the dev implementation (newest first) |

Errors are RFC 7807 problem details with a `code` field:
- 404: unknown workshop, participant or registration
- 409 `AlreadyRegistered`: the participant already has an active registration for this workshop
- 409 `ScheduleConflict`: a confirmed seat would overlap another of the participant's confirmed workshops
- 422 `IdempotencyKeyReused`: the key was already used for a different request
- 400: validation errors

## Architecture

- **`Domain/`**: the plain entities (`Workshop`, `Participant`, `Registration`, `RegistrationStatus`).
- **`Services/RegistrationService`**: all business rules and state changes (register, cancel, waitlist promotion, conflict checks, idempotency). It throws a `DomainException` when a rule is broken. `Program.cs` turns that exception into the HTTP status codes listed above.
- **`Endpoints/`**: thin minimal-API handlers. They validate input, call the service for writes, and project read models (DTOs) straight from EF.
- **`Notifications/`**: `INotificationService` plus `InMemoryNotificationService`, which logs each notification and keeps it in memory. The service sends notifications only after the transaction commits, and wraps each send in try/catch. A notification failure is logged and never changes the result of a registration or cancellation.
- **Concurrency**: every write runs in a SQLite transaction opened with `BEGIN IMMEDIATE`. That takes the database write lock before the capacity, duplicate and idempotency-key checks run, so concurrent requests are serialized and never act on stale reads. Writers that arrive later wait for the lock (Microsoft.Data.Sqlite retries on a busy database). Two filtered unique indexes add a second line of defence:
  - at most one non-cancelled registration per (workshop, participant)
  - unique idempotency keys
- **Persistence**: `EnsureCreated()` builds the schema. There are no migrations, to keep things simple. Times are stored and returned in UTC.

### Tests (`backend/tests/SeatFlow.Tests`, 34 tests)

Each test gets its own real file-backed SQLite database, so locking behaves as it does in production.
- `RegistrationRulesTests` covers:
  - confirm, waitlist and FIFO promotion
  - skipping a waitlisted participant who has a conflict, and leaving them on the waitlist
  - preventing overlapping confirmations, and allowing back-to-back workshops
  - duplicate prevention and re-registering after a cancellation
  - idempotency-key replay and mismatch
  - cancelling twice
  - notification failure without rollback
- `ConcurrencyTests` races 12 participants for the final seat, sends 8 simultaneous requests with the same idempotency key, sends simultaneous duplicate registrations, and sends simultaneous cancellations of the same registration.
- `ApiTests` runs through `WebApplicationFactory`. It covers status codes, validation, the `Idempotency-Key` header, waitlist positions in the schedule and registration endpoints, and concurrent HTTP requests for the last seat.

As a check, switching the transaction to a deferred `BEGIN` makes every concurrency test fail. That confirms the tests really exercise the race.

## Assumptions

- **Conflict on a direct confirmation is rejected.** If a seat is free but the participant has an overlapping confirmed workshop, the request is rejected with 409. They are not silently waitlisted.
- **Waitlisting despite a conflict is allowed.** If the workshop is full, the participant is waitlisted even when they have an overlapping confirmed workshop, because the conflict may be gone by the time a seat opens. Rule 7 skips them during promotion while the conflict remains.
- **Overlap uses half-open intervals.** A workshop ending at 10:00 does not overlap one starting at 10:00.
- **Waitlist order is FIFO by `CreatedAt`**, with the registration id as the tie-breaker. Re-registering after a cancellation creates a new registration, which goes to the back of the queue.
- **Cancelling a waitlisted registration** frees no seat, so nobody is promoted.
- **Cancelling a confirmed registration** also re-checks every workshop where the same participant is waitlisted. Their calendar just opened up, so a seat that was left empty because everyone waiting for it had a conflict can now be filled. Promotion always fills as many free seats as there are eligible people.
- **A free seat goes to a new registrant even if people are waitlisted.** This only happens when every waitlisted person has a conflict, which is consistent with "promote the first eligible participant".
- **Idempotency keys are global and are stored on the registration they created.**
  - Replaying a key with the same workshop and participant returns the original registration in its current state, with status 201 and the header `Idempotent-Replayed: true`. Nothing new is created and no notification is sent.
  - Reusing a key with a different request returns 422.
  - A request that failed (for example with a 409) stores nothing, so retrying it runs the request again.
- **Notifications** are sent for direct confirmations, waitlist promotions and cancellations. Joining a waitlist sends nothing. An idempotent repeat cancel sends nothing.
- **Participants are seed data only.** The spec asks only to register *existing* participants, so there is no endpoint to create them.

## Known limitations

- **One database file and in-process notifications.** The `BEGIN IMMEDIATE` approach serializes all writes. That is fine for a small conference but would not scale to heavy write traffic.
- **Notifications are best-effort.** If the process crashes after commit but before sending, the notification is lost; there is no outbox or retry. Notifications are dispatched after the request's work, on the request thread.
- **The notification log is lost on restart** and keeps only the last 500 entries.
- **No migrations.** Schema changes require deleting `seatflow.db`.
- **No editing or deleting of workshops**, and no capacity changes.
- **Times are shown in the browser's local time zone.** Seed data is on 2026-11-12 (UTC).
- **The UI reloads after each action** rather than updating live, and has no pagination.
