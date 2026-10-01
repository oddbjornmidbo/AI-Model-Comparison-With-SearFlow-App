using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Data;
using SeatFlow.Domain;
using SeatFlow.Services;

namespace SeatFlow.Endpoints;

public static class ApiEndpoints
{
    public static void MapSeatFlowApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/workshops", async (SeatFlowDbContext db) =>
        {
            var workshops = await db.Workshops.AsNoTracking().OrderBy(w => w.StartTime).ThenBy(w => w.Id).ToListAsync();
            var counts = await db.Registrations.AsNoTracking()
                .Where(r => r.Status != RegistrationStatus.Cancelled)
                .GroupBy(r => new { r.WorkshopId, r.Status })
                .Select(g => new { g.Key.WorkshopId, g.Key.Status, Count = g.Count() })
                .ToListAsync();
            int Count(int id, RegistrationStatus s) => counts.FirstOrDefault(c => c.WorkshopId == id && c.Status == s)?.Count ?? 0;
            return workshops.Select(w =>
            {
                var confirmed = Count(w.Id, RegistrationStatus.Confirmed);
                return new WorkshopDto(w.Id, w.Title, w.StartTime, w.EndTime, w.Capacity,
                    confirmed, Count(w.Id, RegistrationStatus.Waitlisted), Math.Max(0, w.Capacity - confirmed));
            });
        });

        api.MapPost("/workshops", async (CreateWorkshopRequest req, SeatFlowDbContext db) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(req.Title)) errors["title"] = ["Title is required."];
            if (req.StartTime is null) errors["startTime"] = ["Start time is required."];
            if (req.EndTime is null) errors["endTime"] = ["End time is required."];
            if (req.StartTime is { } s && req.EndTime is { } e && e <= s) errors["endTime"] = ["End time must be after start time."];
            if (req.Capacity is not > 0) errors["capacity"] = ["Capacity must be at least 1."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var w = new Workshop
            {
                Title = req.Title!.Trim(),
                StartTime = ToUtc(req.StartTime!.Value),
                EndTime = ToUtc(req.EndTime!.Value),
                Capacity = req.Capacity!.Value,
            };
            db.Workshops.Add(w);
            await db.SaveChangesAsync();
            return Results.Created($"/api/workshops/{w.Id}",
                new WorkshopDto(w.Id, w.Title, w.StartTime, w.EndTime, w.Capacity, 0, 0, w.Capacity));
        });

        api.MapGet("/workshops/{id:int}/registrations", async (int id, SeatFlowDbContext db) =>
        {
            if (!await db.Workshops.AnyAsync(w => w.Id == id)) return Results.NotFound();
            var regs = await db.Registrations.AsNoTracking().Include(r => r.Workshop).Include(r => r.Participant)
                .Where(r => r.WorkshopId == id)
                .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .ToListAsync();
            return Results.Ok(regs.Select(RegistrationDto.From));
        });

        api.MapPost("/workshops/{id:int}/registrations", async (int id, RegisterRequest req, HttpContext http, RegistrationService svc) =>
        {
            if (req.ParticipantId is not > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["participantId"] = ["participantId is required."] });

            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
            var result = await svc.RegisterAsync(id, req.ParticipantId.Value, key, http.RequestAborted);
            var dto = RegistrationDto.From(result.Registration);
            if (result.Created) return Results.Created($"/api/registrations/{dto.Id}", dto);

            http.Response.Headers["Idempotent-Replayed"] = "true";
            return Results.Ok(dto);
        });

        api.MapPost("/registrations/{id:int}/cancel", async (int id, RegistrationService svc, CancellationToken ct) =>
            Results.Ok(RegistrationDto.From(await svc.CancelAsync(id, ct))));

        // Every registration of the participant (all statuses) ordered by workshop start, so clients can show states.
        api.MapGet("/participants/{id:int}/schedule", async (int id, SeatFlowDbContext db) =>
        {
            if (!await db.Participants.AnyAsync(p => p.Id == id)) return Results.NotFound();
            var regs = await db.Registrations.AsNoTracking().Include(r => r.Workshop).Include(r => r.Participant)
                .Where(r => r.ParticipantId == id).ToListAsync();
            return Results.Ok(regs.OrderBy(r => r.Workshop.StartTime).ThenBy(r => r.Id).Select(RegistrationDto.From));
        });

        api.MapGet("/participants", async (SeatFlowDbContext db) =>
            await db.Participants.AsNoTracking().OrderBy(p => p.Name).Select(p => new ParticipantDto(p.Id, p.Name, p.Email)).ToListAsync());

        api.MapPost("/participants", async (CreateParticipantRequest req, SeatFlowDbContext db) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(req.Name)) errors["name"] = ["Name is required."];
            if (string.IsNullOrWhiteSpace(req.Email) || !MailAddress.TryCreate(req.Email, out _)) errors["email"] = ["A valid email is required."];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var email = req.Email!.Trim();
            if (await db.Participants.AnyAsync(p => p.Email == email))
                return Results.Problem(statusCode: 409, title: "Conflict", detail: "A participant with this email already exists.");

            var p = new Participant { Name = req.Name!.Trim(), Email = email };
            db.Participants.Add(p);
            await db.SaveChangesAsync();
            return Results.Created($"/api/participants/{p.Id}", new ParticipantDto(p.Id, p.Name, p.Email));
        });

        // Dev-only view of what the in-memory notification service recorded.
        api.MapGet("/notifications", (INotificationService n) =>
            n is InMemoryNotificationService mem ? Results.Ok(mem.Sent) : Results.NotFound());
    }

    private static DateTime ToUtc(DateTime d) => d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();
}
