# SeatFlow

Workshop registrations with capacity, FIFO waitlist, automatic promotion, schedule-conflict protection,
idempotent registration and notifications. Backend: .NET 9 / ASP.NET Core / EF Core / SQLite. Frontend: React + TypeScript + Vite.

## Run the backend

Requires the .NET 9 SDK (a copy was installed project-locally in `.dotnet/` while building this; to use it: `export PATH="$PWD/.dotnet:$PATH"`).

```sh
dotnet run --project src/SeatFlow.Api        # http://localhost:5090
dotnet test                                  # 26 tests
```

The SQLite file `seatflow.db` is created and seeded on first start (delete it to reset). Seed: 5 workshops (several overlapping:
*Domain-Driven Design* 09:00–11:00, *Performance Tuning* 09:30–10:45, *Hands-on Kubernetes* 10:30–12:30; *API Security* and
*Testing React Apps* are back-to-back) and 6 participants. Demo: DDD is full with Carol then Dave waitlisted, but Carol is
confirmed in the overlapping Kubernetes workshop — cancel Alice's DDD registration and Dave is promoted, Carol is skipped.

## Run the frontend

```sh
cd frontend && npm install && npm run dev    # http://localhost:5173, proxies /api to localhost:5090
npm run build                                # production build check
```

## API

| Endpoint | Notes |
|---|---|
| `GET /api/workshops` | with confirmed/waitlisted counts and remaining capacity |
| `POST /api/workshops` | `{title, startTime, endTime, capacity}`; 400 with `errors` on invalid input |
| `GET /api/workshops/{id}/registrations` | all statuses, oldest first (waitlist order = FIFO) |
| `POST /api/workshops/{id}/registrations` | `{participantId}`, optional `Idempotency-Key` header. 201 new, 200 replay (`Idempotent-Replayed: true`), 404, 409 |
| `POST /api/registrations/{id}/cancel` | 200; cancelling again is a no-op 200 |
| `GET /api/participants/{id}/schedule` | all of the participant's registrations (any status) by workshop start |
| `GET/POST /api/participants` | extra: list / create participants |
| `GET /api/notifications` | extra: dev view of recorded notifications |

## Architecture

- `Domain/` entities and `DomainException`. `Data/` EF `DbContext` and seeder.
- `Services/RegistrationService` holds **all** business rules (capacity, waitlist, promotion, conflicts, idempotency). `Endpoints/` is a thin HTTP layer; domain errors map to 404/409/400 in one exception handler.
- `INotificationService` with `InMemoryNotificationService` (logs + records). Notifications are sent **after** the transaction commits and failures are caught/logged, so they can never roll back work.
- Concurrency: seat-affecting operations run under a process-wide `SemaphoreSlim` plus a DB transaction, making check-then-insert atomic. A filtered unique index (one non-cancelled registration per workshop+participant) and a unique index on idempotency keys are database backstops.
- Tests: service-level business tests against real temp-file SQLite (including parallel registrations) plus HTTP tests via `WebApplicationFactory`.

## Assumptions

- Dates are stored/returned as UTC. Workshops that merely touch (end == start) do not overlap.
- Registering when a seat is free but the participant already has an overlapping **confirmed** workshop is rejected with 409 (rather than waitlisted). If the workshop is full, the participant is waitlisted regardless; the conflict is evaluated at promotion time (rule 7).
- Idempotency: the key is tied to (workshop, participant); reusing it for a different request gives 409. A replay returns the original registration with its *current* status (even if later cancelled/promoted) and sends no new notification. Keys are never expired.
- Duplicate prevention applies to Confirmed and Waitlisted registrations; a new registration (new id) is allowed after cancelling.
- Only one waitlisted person is promoted per freed seat; if everyone is conflicted the seat stays free and new registrants can take it.
- The schedule endpoint returns all statuses so the UI can show Cancelled/Waitlisted too.
- No auth; participants are pre-existing (seeded or created via API). Schema via `EnsureCreated`, no migrations.

## Known limitations

- The concurrency lock is in-process: correct for a single API instance only.
- Notifications are fire-after-commit, with no retry/outbox; a crash between commit and send loses them.
- Idempotency records are not purged; no request-body hash beyond workshop/participant.
- UI has no workshop/participant creation forms (use the API) and no live updates (it refreshes after each action).
