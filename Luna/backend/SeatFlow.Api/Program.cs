using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using SeatFlow.Api.Contracts;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("SeatFlow") ?? "Data Source=seatflow.db";
builder.Services.AddDbContextFactory<SeatFlowDbContext>(options => options.UseSqlite(connectionString));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<INotificationService, DevelopmentNotificationService>();
builder.Services.AddSingleton<RegistrationService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:8200", "http://127.0.0.1:8200")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
await SeedData.InitializeAsync(app.Services.GetRequiredService<IDbContextFactory<SeatFlowDbContext>>());

app.MapGet("/api/workshops", async (IDbContextFactory<SeatFlowDbContext> factory, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    var workshops = await db.Workshops.AsNoTracking().OrderBy(x => x.StartTime).ToListAsync(cancellationToken);
    var counts = await db.Registrations.AsNoTracking().Where(x => x.Status == RegistrationStatus.Confirmed)
        .GroupBy(x => x.WorkshopId).Select(group => new { WorkshopId = group.Key, Count = group.Count() }).ToDictionaryAsync(x => x.WorkshopId, x => x.Count, cancellationToken);
    return Results.Ok(workshops.Select(x => x.ToDto(counts.GetValueOrDefault(x.Id))));
});

app.MapGet("/api/workshops/{id:int}", async (int id, IDbContextFactory<SeatFlowDbContext> factory, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    var workshop = await db.Workshops.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (workshop is null)
        return Results.NotFound();
    var confirmedCount = await db.Registrations.CountAsync(x => x.WorkshopId == id && x.Status == RegistrationStatus.Confirmed, cancellationToken);
    return Results.Ok(workshop.ToDto(confirmedCount));
});

app.MapPost("/api/workshops", async (CreateWorkshopRequest request, IDbContextFactory<SeatFlowDbContext> factory, CancellationToken cancellationToken) =>
{
    var title = request.Title?.Trim();
    if (string.IsNullOrWhiteSpace(title) || title.Length > 160 || request.StartTime >= request.EndTime || request.Capacity < 1)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["workshop"] = ["Provide a title, a start time before the end time, and a positive capacity."] });
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    var workshop = new Workshop { Title = title, StartTime = request.StartTime.ToUniversalTime(), EndTime = request.EndTime.ToUniversalTime(), Capacity = request.Capacity };
    db.Workshops.Add(workshop);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/workshops/{workshop.Id}", workshop.ToDto(0));
});

app.MapGet("/api/participants", async (IDbContextFactory<SeatFlowDbContext> factory, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    var participants = await db.Participants.AsNoTracking().OrderBy(x => x.Name).Select(x => new ParticipantDto(x.Id, x.Name, x.Email)).ToListAsync(cancellationToken);
    return Results.Ok(participants);
});

app.MapGet("/api/workshops/{id:int}/registrations", async (int id, IDbContextFactory<SeatFlowDbContext> factory, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    if (!await db.Workshops.AnyAsync(x => x.Id == id, cancellationToken))
        return Results.NotFound();
    var registrations = await db.Registrations.AsNoTracking().Include(x => x.Participant).Where(x => x.WorkshopId == id)
        .OrderBy(x => x.Status == RegistrationStatus.Confirmed ? 0 : x.Status == RegistrationStatus.Waitlisted ? 1 : 2)
        .ThenBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    return Results.Ok(registrations.Select(x => x.ToDto()));
});

app.MapPost("/api/workshops/{id:int}/registrations", async (int id, RegisterRequest request, HttpRequest httpRequest, RegistrationService service, CancellationToken cancellationToken) =>
{
    var key = httpRequest.Headers["Idempotency-Key"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(key) || key.Length > 200)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A key between 1 and 200 characters is required."] });
    if (request.ParticipantId <= 0)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["participantId"] = ["Participant ID must be positive."] });
    var result = await service.RegisterAsync(id, request.ParticipantId, key, cancellationToken);
    if (result.Failure != RegistrationFailure.None)
        return RegistrationError(result.Failure);
    var response = result.Registration!.ToDto();
    return result.WasReplayed ? Results.Ok(response) : Results.Created($"/api/workshops/{id}/registrations/{response.Id}", response);
});

app.MapPost("/api/registrations/{id:int}/cancel", async (int id, RegistrationService service, CancellationToken cancellationToken) =>
{
    var result = await service.CancelAsync(id, cancellationToken);
    if (result.Failure != RegistrationFailure.None)
        return RegistrationError(result.Failure);
    return Results.Ok(new
    {
        registration = result.Registration!.ToDto(),
        promoted = result.Promoted?.ToDto(),
        alreadyCancelled = result.WasAlreadyCancelled
    });
});

app.MapGet("/api/participants/{id:int}/schedule", async (int id, IDbContextFactory<SeatFlowDbContext> factory, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    if (!await db.Participants.AnyAsync(x => x.Id == id, cancellationToken))
        return Results.NotFound();
    var schedule = await db.Registrations.AsNoTracking().Where(x => x.ParticipantId == id && x.Status == RegistrationStatus.Confirmed)
        .OrderBy(x => x.Workshop!.StartTime)
        .Select(x => new ScheduleItemDto(x.Id, x.WorkshopId, x.Workshop!.Title, x.Workshop.StartTime, x.Workshop.EndTime))
        .ToListAsync(cancellationToken);
    return Results.Ok(schedule);
});

app.Run();

static IResult RegistrationError(RegistrationFailure failure) => failure switch
{
    RegistrationFailure.WorkshopNotFound or RegistrationFailure.ParticipantNotFound or RegistrationFailure.RegistrationNotFound => Results.NotFound(new { error = failure.ToString() }),
    RegistrationFailure.AlreadyRegistered => Results.Conflict(new { error = "AlreadyRegistered", detail = "This participant already has an active registration for the workshop." }),
    RegistrationFailure.ScheduleConflict => Results.Conflict(new { error = "ScheduleConflict", detail = "This confirmed registration would overlap another confirmed workshop." }),
    RegistrationFailure.IdempotencyKeyConflict => Results.Conflict(new { error = "IdempotencyKeyConflict", detail = "This Idempotency-Key was already used for a different request." }),
    _ => Results.Problem("The registration operation could not be completed.")
};

public partial class Program;
