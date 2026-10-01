# SeatFlow

SeatFlow is a small workshop registration app for a conference. It includes an ASP.NET Core Web API backed by EF Core and SQLite, an xUnit business behavior suite, and a React/TypeScript interface.

## Run the backend

Install the .NET 9 SDK, then from the repository root run:

```sh
dotnet run --project backend/SeatFlow.Api -- --urls http://localhost:5100
```

Check that `dotnet --version` reports 9.x. On this development host the .NET 9 SDK is under `~/.dotnet`; if the default `dotnet` command selects .NET 8, add that directory to `PATH` for the shell first (`export PATH="$HOME/.dotnet:$PATH"`).

The API creates `seatflow.db` in its working directory when it starts and seeds the workshops and participants on the first run. It exposes `GET /api/workshops`, `POST /api/workshops`, `GET /api/participants`, `GET /api/workshops/{id}/registrations`, `POST /api/workshops/{id}/registrations`, `POST /api/registrations/{id}/cancel`, and `GET /api/participants/{id}/schedule`.

## Run the frontend

Use Node.js 20 or newer. In a second terminal:

```sh
cd frontend
npm install
npm run dev
```

Open the Vite URL shown in the terminal (normally <http://localhost:5173>). The API base URL defaults to `http://localhost:5100`; override it with `VITE_API_BASE_URL` when needed.

## Run tests and builds

```sh
dotnet test backend/SeatFlow.Tests/SeatFlow.Tests.csproj
dotnet build backend/SeatFlow.Api/SeatFlow.Api.csproj
cd frontend && npm run build
```

## Architecture

- `backend/SeatFlow.Api/Domain` holds the workshop, participant, and registration model.
- `RegistrationService` owns registration, cancellation, seat assignment, schedule checks, waitlist promotion, and idempotency behavior. Controllers/endpoints stay thin.
- EF Core maps the app to SQLite. Times are stored as UTC ticks so overlap and ordering comparisons work consistently in SQLite.
- `INotificationService` is the notification boundary. The development implementation records notifications in memory and writes them to logs. Notification errors are caught after the database transaction commits.
- `frontend/src` contains the single-page UI and typed API client.

## Assumptions

- A participant may hold only one non-cancelled registration per workshop. A cancelled registration allows a new registration, which gets a new creation time and idempotency key.
- Schedule conflicts are rejected only when the requested registration would be confirmed. A waitlisted registration can remain in line; eligibility is checked again during promotion.
- Workshop intervals use half-open boundaries: a workshop ending at 11:00 does not conflict with one starting at 11:00.
- Reusing an `Idempotency-Key` with the same workshop and participant returns the original registration, including its current status. Reusing it for a different request returns 409.
- Capacity allocation and promotions are serialized across service instances in this API process. This suits the single-instance local SQLite app; running multiple API processes against one database would need a cross-process coordination strategy.
- Seeded workshop dates are relative to the first startup, and the seed runs only when there are no workshops in the database. Reset the local database file to reseed.

## Known limitations

- This is a local development app: it has no authentication, deployment setup, migrations, or external notification delivery.
- The UI uses the seeded participant list and does not include participant management.
