using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Api;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Notifications;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SeatFlowDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SeatFlow")));
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddSingleton<INotificationService, InMemoryNotificationService>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173", "http://localhost:6006").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.MapSeatFlowEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SeatFlowDbContext>();
    db.Database.EnsureCreated();
    // WAL lets readers proceed while a registration holds the write lock.
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    if (app.Configuration.GetValue("Seed:Enabled", true))
    {
        SeedData.SeedIfEmpty(db);
    }
}

app.Run();

public partial class Program;
