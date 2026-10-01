using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Data;
using SeatFlow.Domain;
using SeatFlow.Endpoints;
using SeatFlow.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SeatFlowDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("SeatFlow") ?? "Data Source=seatflow.db"));
builder.Services.AddSingleton<INotificationService, InMemoryNotificationService>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var app = builder.Build();

// Map domain errors to HTTP status codes.
app.UseExceptionHandler(handler => handler.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = ex is DomainException d
        ? d.Kind switch
        {
            DomainErrorKind.NotFound => (404, "Not found"),
            DomainErrorKind.Conflict => (409, "Conflict"),
            _ => (400, "Validation error"),
        }
        : (500, "Unexpected error");
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { status, title, detail = ex is DomainException ? ex.Message : null });
}));

// Tests create their own empty database.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    DbSeeder.Seed(scope.ServiceProvider.GetRequiredService<SeatFlowDbContext>());
}

app.MapSeatFlowApi();
app.Run();

public partial class Program;
