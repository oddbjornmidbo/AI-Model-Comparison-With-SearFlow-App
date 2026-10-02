using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SeatFlow.Api.Api;
using SeatFlow.Api.Notifications;

namespace SeatFlow.Tests;

/// <summary>Runs the real API against its own temporary SQLite file, without seed data.</summary>
public sealed class SeatFlowApp : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"seatflow-test-{Guid.NewGuid():N}.db");

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public RecordingNotificationService Notifications { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:SeatFlow", $"Data Source={_dbPath};Default Timeout=30");
        builder.UseSetting("Seed:Enabled", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<INotificationService>();
            services.AddSingleton<INotificationService>(Notifications);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(_dbPath + suffix);
        }
    }

    public async Task<WorkshopDto> CreateWorkshopAsync(string title, int capacity, int startHour = 9, int durationHours = 1)
    {
        var start = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero).AddHours(startHour);
        var response = await CreateClient().PostAsJsonAsync("/api/workshops",
            new { title, startTime = start, endTime = start.AddHours(durationHours), capacity });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkshopDto>(Json))!;
    }

    public async Task<ParticipantDto> CreateParticipantAsync(string name)
    {
        var response = await CreateClient().PostAsJsonAsync("/api/participants",
            new { name, email = $"{name.Replace(' ', '.')}.{Guid.NewGuid():N}@example.com" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ParticipantDto>(Json))!;
    }

    public Task<HttpResponseMessage> PostRegistrationAsync(int workshopId, int participantId, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/workshops/{workshopId}/registrations")
        {
            Content = JsonContent.Create(new { participantId }),
        };
        if (idempotencyKey is not null) request.Headers.Add(Endpoints.IdempotencyKeyHeader, idempotencyKey);
        return CreateClient().SendAsync(request);
    }

    public async Task<RegistrationDto> RegisterAsync(int workshopId, int participantId)
    {
        var response = await PostRegistrationAsync(workshopId, participantId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegistrationDto>(Json))!;
    }

    public async Task<RegistrationDto> CancelAsync(int registrationId)
    {
        var response = await CreateClient().PostAsync($"/api/registrations/{registrationId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegistrationDto>(Json))!;
    }

    public async Task<List<RegistrationDto>> GetRegistrationsAsync(int workshopId) =>
        (await CreateClient().GetFromJsonAsync<List<RegistrationDto>>($"/api/workshops/{workshopId}/registrations", Json))!;

    public async Task<WorkshopDto> GetWorkshopAsync(int workshopId) =>
        (await CreateClient().GetFromJsonAsync<WorkshopDto>($"/api/workshops/{workshopId}", Json))!;

    public async Task<ScheduleDto> GetScheduleAsync(int participantId) =>
        (await CreateClient().GetFromJsonAsync<ScheduleDto>($"/api/participants/{participantId}/schedule", Json))!;
}

public sealed class RecordingNotificationService : INotificationService
{
    private readonly List<Notification> _sent = [];

    public bool Fail { get; set; }

    public IReadOnlyList<Notification> Sent
    {
        get { lock (_sent) return _sent.ToList(); }
    }

    public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        if (Fail) throw new InvalidOperationException("SMTP server is down");
        lock (_sent) _sent.Add(notification);
        return Task.CompletedTask;
    }
}
