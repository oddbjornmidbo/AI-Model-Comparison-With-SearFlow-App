using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Notifications;
using SeatFlow.Api.Services;

namespace SeatFlow.Api.Endpoints;

public static class SeatFlowEndpoints
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    private const int MaxIdempotencyKeyLength = 200;

    public static void MapSeatFlowEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/workshops", ListWorkshops);
        api.MapPost("/workshops", CreateWorkshop);
        api.MapGet("/workshops/{id:int}/registrations", ListWorkshopRegistrations);
        api.MapPost("/workshops/{id:int}/registrations", Register);
        api.MapGet("/registrations/{id:int}", GetRegistration);
        api.MapPost("/registrations/{id:int}/cancel", Cancel);
        api.MapGet("/participants", ListParticipants);
        api.MapGet("/participants/{id:int}/schedule", GetSchedule);
        api.MapGet("/notifications", ListNotifications);
    }

    private static async Task<List<WorkshopDto>> ListWorkshops(SeatFlowDbContext db, CancellationToken ct) =>
        await db.Workshops
            .OrderBy(w => w.StartTime).ThenBy(w => w.Id)
            .Select(ToWorkshopDto)
            .ToListAsync(ct);

    private static async Task<Results<Created<WorkshopDto>, ValidationProblem>> CreateWorkshop(
        CreateWorkshopRequest request, SeatFlowDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var title = request.Title?.Trim();
        if (string.IsNullOrEmpty(title)) errors["title"] = ["Title is required."];
        else if (title.Length > 200) errors["title"] = ["Title must be at most 200 characters."];
        if (request.StartTime is null) errors["startTime"] = ["Start time is required."];
        if (request.EndTime is null) errors["endTime"] = ["End time is required."];
        else if (request.StartTime is not null && request.EndTime <= request.StartTime)
            errors["endTime"] = ["End time must be after start time."];
        if (request.Capacity is null or < 1) errors["capacity"] = ["Capacity must be at least 1."];
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var workshop = new Workshop
        {
            Title = title!,
            StartTime = request.StartTime!.Value.ToUniversalTime(),
            EndTime = request.EndTime!.Value.ToUniversalTime(),
            Capacity = request.Capacity!.Value,
        };
        db.Workshops.Add(workshop);
        await db.SaveChangesAsync(ct);

        var dto = new WorkshopDto(workshop.Id, workshop.Title, workshop.StartTime, workshop.EndTime,
            workshop.Capacity, 0, 0, workshop.Capacity);
        return TypedResults.Created($"/api/workshops/{workshop.Id}", dto);
    }

    private static async Task<Results<Ok<List<RegistrationDto>>, NotFound<ProblemDetails>>> ListWorkshopRegistrations(
        int id, SeatFlowDbContext db, CancellationToken ct)
    {
        if (!await db.Workshops.AnyAsync(w => w.Id == id, ct))
            return NotFound($"Workshop {id} was not found.");

        var registrations = await db.Registrations
            .Include(r => r.Participant)
            .Where(r => r.WorkshopId == id)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .ToListAsync(ct);

        var position = 0;
        var result = registrations
            .Select(r => RegistrationDto.From(r, r.Status == RegistrationStatus.Waitlisted ? ++position : null))
            .ToList();
        return TypedResults.Ok(result);
    }

    private static async Task<IResult> Register(
        int id,
        CreateRegistrationRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        HttpContext http,
        RegistrationService service,
        SeatFlowDbContext db,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ParticipantId is null or < 1)
            errors["participantId"] = ["A valid participantId is required."];
        if (idempotencyKey is not null && (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > MaxIdempotencyKeyLength))
            errors[IdempotencyKeyHeader] = [$"Idempotency-Key must be 1-{MaxIdempotencyKeyLength} non-blank characters."];
        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var result = await service.RegisterAsync(id, request.ParticipantId!.Value, idempotencyKey, ct);
        if (result.Replayed)
            http.Response.Headers["Idempotent-Replayed"] = "true";

        var registration = result.Registration;
        var dto = RegistrationDto.From(registration, await WaitlistPositionAsync(db, registration, ct));
        return TypedResults.Created($"/api/registrations/{registration.Id}", dto);
    }

    private static async Task<Results<Ok<RegistrationDto>, NotFound<ProblemDetails>>> GetRegistration(
        int id, SeatFlowDbContext db, CancellationToken ct)
    {
        var registration = await db.Registrations.Include(r => r.Participant).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (registration is null)
            return NotFound($"Registration {id} was not found.");
        return TypedResults.Ok(RegistrationDto.From(registration, await WaitlistPositionAsync(db, registration, ct)));
    }

    private static async Task<Ok<CancelResponse>> Cancel(int id, RegistrationService service, CancellationToken ct)
    {
        var result = await service.CancelAsync(id, ct);
        return TypedResults.Ok(new CancelResponse(
            RegistrationDto.From(result.Registration),
            result.AlreadyCancelled,
            result.Promoted.Select(p => RegistrationDto.From(p)).ToList()));
    }

    private static async Task<List<ParticipantDto>> ListParticipants(SeatFlowDbContext db, CancellationToken ct) =>
        await db.Participants
            .OrderBy(p => p.Name)
            .Select(p => new ParticipantDto(p.Id, p.Name, p.Email))
            .ToListAsync(ct);

    private static async Task<Results<Ok<ScheduleDto>, NotFound<ProblemDetails>>> GetSchedule(
        int id, SeatFlowDbContext db, CancellationToken ct)
    {
        var participant = await db.Participants.FindAsync([id], ct);
        if (participant is null)
            return NotFound($"Participant {id} was not found.");

        var registrations = await db.Registrations
            .Include(r => r.Workshop)
            .Where(r => r.ParticipantId == id)
            .OrderBy(r => r.Workshop.StartTime).ThenBy(r => r.CreatedAt)
            .ToListAsync(ct);

        var items = new List<ScheduleItemDto>();
        foreach (var r in registrations)
        {
            items.Add(new ScheduleItemDto(r.Id, r.Status, r.WorkshopId, r.Workshop.Title,
                r.Workshop.StartTime, r.Workshop.EndTime, r.CreatedAt, await WaitlistPositionAsync(db, r, ct)));
        }

        return TypedResults.Ok(new ScheduleDto(new ParticipantDto(participant.Id, participant.Name, participant.Email), items));
    }

    private static IReadOnlyList<Notification> ListNotifications(InMemoryNotificationService notifications) =>
        notifications.Sent.Reverse().ToList();

    private static readonly System.Linq.Expressions.Expression<Func<Workshop, WorkshopDto>> ToWorkshopDto = w => new WorkshopDto(
        w.Id, w.Title, w.StartTime, w.EndTime, w.Capacity,
        w.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed),
        w.Registrations.Count(r => r.Status == RegistrationStatus.Waitlisted),
        w.Capacity - w.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed));

    /// <summary>1-based FIFO position on the workshop's waitlist, or null when not waitlisted.</summary>
    private static async Task<int?> WaitlistPositionAsync(SeatFlowDbContext db, Registration r, CancellationToken ct)
    {
        if (r.Status != RegistrationStatus.Waitlisted)
            return null;
        var ahead = await db.Registrations.CountAsync(o =>
            o.WorkshopId == r.WorkshopId &&
            o.Status == RegistrationStatus.Waitlisted &&
            (o.CreatedAt < r.CreatedAt || (o.CreatedAt == r.CreatedAt && o.Id < r.Id)), ct);
        return ahead + 1;
    }

    private static NotFound<ProblemDetails> NotFound(string detail) =>
        TypedResults.NotFound(new ProblemDetails { Title = "Not found", Status = 404, Detail = detail });
}
