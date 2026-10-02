using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Notifications;

namespace SeatFlow.Api.Api;

public static class Endpoints
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    private const int MaxIdempotencyKeyLength = 200;

    public static void MapSeatFlowEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/workshops", async (SeatFlowDbContext db, CancellationToken ct) =>
            await ToWorkshopDtos(db.Workshops.OrderBy(w => w.StartTime).ThenBy(w => w.Id)).ToListAsync(ct));

        api.MapGet("/workshops/{id:int}", async (int id, SeatFlowDbContext db, CancellationToken ct) =>
            await ToWorkshopDtos(db.Workshops.Where(w => w.Id == id)).SingleOrDefaultAsync(ct) is { } workshop
                ? Results.Ok(workshop)
                : NotFound($"Workshop {id} was not found."));

        api.MapPost("/workshops", async (CreateWorkshopRequest request, SeatFlowDbContext db, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Title)) errors["title"] = ["Title is required."];
            else if (request.Title.Trim().Length > 200) errors["title"] = ["Title must be at most 200 characters."];
            if (request.StartTime is null) errors["startTime"] = ["Start time is required."];
            if (request.EndTime is null) errors["endTime"] = ["End time is required."];
            else if (request.StartTime is not null && request.EndTime <= request.StartTime)
                errors["endTime"] = ["End time must be after start time."];
            if (request.Capacity is null or < 1) errors["capacity"] = ["Capacity must be at least 1."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var workshop = new Workshop
            {
                Title = request.Title!.Trim(),
                StartTime = request.StartTime!.Value.UtcDateTime,
                EndTime = request.EndTime!.Value.UtcDateTime,
                Capacity = request.Capacity!.Value,
            };
            db.Workshops.Add(workshop);
            await db.SaveChangesAsync(ct);

            var dto = new WorkshopDto(workshop.Id, workshop.Title, workshop.StartTime, workshop.EndTime,
                workshop.Capacity, 0, 0, workshop.Capacity);
            return Results.Created($"/api/workshops/{workshop.Id}", dto);
        });

        api.MapGet("/workshops/{id:int}/registrations", async (int id, SeatFlowDbContext db, CancellationToken ct) =>
        {
            if (!await db.Workshops.AnyAsync(w => w.Id == id, ct)) return NotFound($"Workshop {id} was not found.");

            var registrations = await db.Registrations
                .Include(r => r.Participant)
                .Where(r => r.WorkshopId == id)
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .ToListAsync(ct);

            // Already in FIFO order, so the waitlist position is the running count of waitlisted entries.
            var position = 0;
            return Results.Ok(registrations.Select(r => ToDto(r,
                r.Status == RegistrationStatus.Waitlisted ? ++position : null)).ToList());
        });

        api.MapPost("/workshops/{id:int}/registrations", async (
            int id,
            CreateRegistrationRequest request,
            HttpRequest http,
            RegistrationService service,
            SeatFlowDbContext db,
            CancellationToken ct) =>
        {
            if (request.ParticipantId is null or < 1)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["participantId"] = ["A valid participant id is required."],
                });
            }

            string? key = http.Headers[IdempotencyKeyHeader];
            if (key is not null && (string.IsNullOrWhiteSpace(key) || key.Length > MaxIdempotencyKeyLength))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [IdempotencyKeyHeader] = [$"Idempotency-Key must be 1-{MaxIdempotencyKeyLength} characters."],
                });
            }

            var result = await service.RegisterAsync(id, request.ParticipantId.Value, key, ct);
            if (result.Replayed) http.HttpContext.Response.Headers["Idempotent-Replayed"] = "true";

            var dto = ToDto(result.Registration, await WaitlistPositionAsync(db, result.Registration, ct));
            return Results.Created($"/api/registrations/{dto.Id}", dto);
        });

        api.MapGet("/registrations/{id:int}", async (int id, SeatFlowDbContext db, CancellationToken ct) =>
        {
            var registration = await db.Registrations.Include(r => r.Participant).SingleOrDefaultAsync(r => r.Id == id, ct);
            return registration is null
                ? NotFound($"Registration {id} was not found.")
                : Results.Ok(ToDto(registration, await WaitlistPositionAsync(db, registration, ct)));
        });

        api.MapPost("/registrations/{id:int}/cancel", async (int id, RegistrationService service, CancellationToken ct) =>
        {
            var result = await service.CancelAsync(id, ct);
            return Results.Ok(ToDto(result.Registration, null));
        });

        api.MapGet("/participants", async (SeatFlowDbContext db, CancellationToken ct) =>
            await db.Participants.OrderBy(p => p.Name)
                .Select(p => new ParticipantDto(p.Id, p.Name, p.Email))
                .ToListAsync(ct));

        api.MapPost("/participants", async (CreateParticipantRequest request, SeatFlowDbContext db, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Name)) errors["name"] = ["Name is required."];
            if (string.IsNullOrWhiteSpace(request.Email) || !MailAddress.TryCreate(request.Email.Trim(), out _))
                errors["email"] = ["A valid e-mail address is required."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var email = request.Email!.Trim().ToLowerInvariant();
            if (await db.Participants.AnyAsync(p => p.Email == email, ct))
            {
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict",
                    detail: $"A participant with e-mail {email} already exists.",
                    extensions: new Dictionary<string, object?> { ["code"] = "duplicate_participant" });
            }

            var participant = new Participant { Name = request.Name!.Trim(), Email = email };
            db.Participants.Add(participant);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/participants/{participant.Id}",
                new ParticipantDto(participant.Id, participant.Name, participant.Email));
        });

        api.MapGet("/participants/{id:int}/schedule", async (int id, SeatFlowDbContext db, CancellationToken ct) =>
        {
            var participant = await db.Participants.FindAsync([id], ct);
            if (participant is null) return NotFound($"Participant {id} was not found.");

            var registrations = await db.Registrations
                .Include(r => r.Workshop)
                .Where(r => r.ParticipantId == id)
                .ToListAsync(ct);

            var items = new List<ScheduleItemDto>();
            foreach (var r in registrations.OrderBy(r => r.Workshop.StartTime).ThenBy(r => r.CreatedAt))
            {
                items.Add(new ScheduleItemDto(r.Id, r.Status, r.CreatedAt, await WaitlistPositionAsync(db, r, ct),
                    new WorkshopSummaryDto(r.Workshop.Id, r.Workshop.Title, r.Workshop.StartTime, r.Workshop.EndTime)));
            }

            return Results.Ok(new ScheduleDto(new ParticipantDto(participant.Id, participant.Name, participant.Email), items));
        });

        // Development aid: inspect what the in-memory notification service has "sent".
        api.MapGet("/notifications", (INotificationService notifications) =>
            notifications is InMemoryNotificationService inMemory
                ? Results.Ok(inMemory.Sent.Reverse())
                : Results.NotFound());
    }

    // Projection must come last: EF cannot filter or sort on a record constructed in the projection.
    private static IQueryable<WorkshopDto> ToWorkshopDtos(IQueryable<Workshop> workshops) =>
        workshops.Select(w => new WorkshopDto(w.Id, w.Title, w.StartTime, w.EndTime, w.Capacity,
            w.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed),
            w.Registrations.Count(r => r.Status == RegistrationStatus.Waitlisted),
            Math.Max(0, w.Capacity - w.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed))));

    private static async Task<int?> WaitlistPositionAsync(SeatFlowDbContext db, Registration r, CancellationToken ct)
    {
        if (r.Status != RegistrationStatus.Waitlisted) return null;
        var ahead = await db.Registrations.CountAsync(o =>
            o.WorkshopId == r.WorkshopId
            && o.Status == RegistrationStatus.Waitlisted
            && (o.CreatedAt < r.CreatedAt || (o.CreatedAt == r.CreatedAt && o.Id < r.Id)), ct);
        return ahead + 1;
    }

    private static RegistrationDto ToDto(Registration r, int? waitlistPosition) =>
        new(r.Id, r.WorkshopId, r.ParticipantId, r.Participant.Name, r.Participant.Email, r.Status, r.CreatedAt, waitlistPosition);

    private static IResult NotFound(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = "not_found" });
}
