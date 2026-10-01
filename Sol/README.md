# SeatFlow

SeatFlow is a small workshop registration application with a .NET 9 API, SQLite storage, a React/TypeScript UI, and xUnit behavior tests.

## Run

Prerequisites: .NET 9 SDK and Node.js 18 or newer.

Backend (from this directory):

```sh
dotnet run --project backend/SeatFlow.Api --urls http://localhost:5080
```

Frontend (in a second terminal):

```sh
cd frontend
npm install
npm run dev
```

Open `http://localhost:5173`. The SQLite file is created automatically on the API's first run. Seed data includes four workshops and five participants. The UI uses the seeded participants; `GET /api/participants` provides the participant list.

Build and test:

```sh
dotnet build backend/SeatFlow.Api/SeatFlow.Api.csproj
dotnet test backend/SeatFlow.Tests/SeatFlow.Tests.csproj
cd frontend && npm run build
```

## Architecture

The API endpoints live in `backend/SeatFlow.Api/Program.cs`. `RegistrationService` owns registration, cancellation, capacity, overlap, and waitlist rules. EF Core persists them in SQLite. A transaction covers each booking change; SQLite serializes database writers, and an in-process gate keeps concurrent requests ordered. A unique index protects idempotency keys. Notifications are sent after commit through `INotificationService`; the development service records them in memory and logs them. Notification errors are logged without reversing the booking.

The frontend fetches workshop and participant data from the API through Vite's local proxy. It shows confirmed, waitlisted, and cancelled registrations, and a selected participant's confirmed schedule.

## Assumptions and limitations

- Workshop time ranges are half-open: an end time equal to another start time does not conflict. Send API times as ISO 8601 UTC values. The UI displays them in the browser's local time.
- A participant with a conflicting confirmed workshop may join a **full** workshop's waitlist. Conflict is checked before confirmation and again during promotion. A conflicting participant is rejected when an immediate seat is available.
- A cancelled registration does not block a new registration. Replaying its original idempotency key still returns that original registration; use a new key for a new attempt.
- FIFO order uses creation time, then registration ID for ties.
- The database is created with EF Core `EnsureCreated`; schema migrations are outside this small application's scope. Delete the SQLite file after changing the model schema.
- Participants are seeded and selectable but cannot be created in the UI or API.
