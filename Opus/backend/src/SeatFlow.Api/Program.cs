using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Endpoints;
using SeatFlow.Api.Notifications;
using SeatFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SeatFlowDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("SeatFlow") ?? "Data Source=seatflow.db"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<InMemoryNotificationService>();
builder.Services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<InMemoryNotificationService>());
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SeatFlowDbContext>();
    db.Database.EnsureCreated();
    if (app.Configuration.GetValue("Seed:Enabled", true))
        SeedData.Seed(db);
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapSeatFlowEndpoints();

app.Run();

/// <summary>Maps domain rule violations to RFC 7807 problem responses.</summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is not DomainException domain)
            return false;

        var (status, title) = domain.Error switch
        {
            DomainError.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            DomainError.AlreadyRegistered => (StatusCodes.Status409Conflict, "Already registered"),
            DomainError.ScheduleConflict => (StatusCodes.Status409Conflict, "Schedule conflict"),
            DomainError.IdempotencyKeyReused => (StatusCodes.Status422UnprocessableEntity, "Idempotency-Key reused"),
            _ => (StatusCodes.Status400BadRequest, "Bad request"),
        };

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = domain.Message,
                Extensions = { ["code"] = domain.Error.ToString() },
            },
        });
    }
}

public partial class Program;
