using System.Net;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace SeatFlow.Tests;

public class ApiTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"seatflow-api-{Guid.NewGuid():N}.db");
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:SeatFlow", $"Data Source={_dbPath}");
        });
        // Creating the schema in the test database (Testing environment skips seeding).
        using (var scope = _factory.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<SeatFlow.Data.SeatFlowDbContext>().Database.EnsureCreated();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
    }

    private async Task<int> CreateWorkshop(int capacity, int startHour = 9, int endHour = 10)
    {
        var res = await _client.PostAsJsonAsync("/api/workshops", new
        {
            title = "W", startTime = $"2030-01-01T{startHour:00}:00:00Z", endTime = $"2030-01-01T{endHour:00}:00:00Z", capacity,
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<int> CreateParticipant(string name)
    {
        var res = await _client.PostAsJsonAsync("/api/participants", new { name, email = $"{name}@example.com" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private Task<HttpResponseMessage> Register(int workshop, int participant, string? key = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/workshops/{workshop}/registrations")
        {
            Content = JsonContent.Create(new { participantId = participant }),
        };
        if (key != null) req.Headers.Add("Idempotency-Key", key);
        return _client.SendAsync(req);
    }

    [Fact]
    public async Task Workshop_validation_returns_400_with_errors()
    {
        var res = await _client.PostAsJsonAsync("/api/workshops", new
        {
            title = "", startTime = "2030-01-01T10:00:00Z", endTime = "2030-01-01T09:00:00Z", capacity = 0,
        });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("endTime", out _));
        Assert.True(errors.TryGetProperty("capacity", out _));
    }

    [Fact]
    public async Task Register_returns_201_then_idempotent_replay_returns_200_with_same_registration()
    {
        var w = await CreateWorkshop(capacity: 2);
        var p = await CreateParticipant("alice");

        var first = await Register(w, p, "abc");
        var replay = await Register(w, p, "abc");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var a = await first.Content.ReadFromJsonAsync<JsonElement>();
        var b = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a.GetProperty("id").GetInt32(), b.GetProperty("id").GetInt32());

        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/workshops/{w}/registrations");
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task Duplicate_registration_without_key_returns_409()
    {
        var w = await CreateWorkshop(capacity: 2);
        var p = await CreateParticipant("alice");
        await Register(w, p);

        Assert.Equal(HttpStatusCode.Conflict, (await Register(w, p)).StatusCode);
    }

    [Fact]
    public async Task Unknown_workshop_or_participant_returns_404_and_missing_participant_id_400()
    {
        var w = await CreateWorkshop(capacity: 2);
        Assert.Equal(HttpStatusCode.NotFound, (await Register(9999, 1)).StatusCode);
        var p = await CreateParticipant("alice");
        Assert.Equal(HttpStatusCode.NotFound, (await Register(w, 9999)).StatusCode);

        var res = await _client.PostAsJsonAsync($"/api/workshops/{w}/registrations", new { });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/workshops/9999/registrations")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/participants/9999/schedule")).StatusCode);
        _ = p;
    }

    [Fact]
    public async Task Full_flow_waitlist_cancel_promote_and_schedule()
    {
        var w = await CreateWorkshop(capacity: 1);
        var alice = await CreateParticipant("alice");
        var bob = await CreateParticipant("bob");

        var aReg = (await (await Register(w, alice)).Content.ReadFromJsonAsync<JsonElement>());
        var bReg = (await (await Register(w, bob)).Content.ReadFromJsonAsync<JsonElement>());
        Assert.Equal("Confirmed", aReg.GetProperty("status").GetString());
        Assert.Equal("Waitlisted", bReg.GetProperty("status").GetString());

        var cancel = await _client.PostAsync($"/api/registrations/{aReg.GetProperty("id").GetInt32()}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/registrations/{aReg.GetProperty("id").GetInt32()}/cancel", null)).StatusCode);

        var schedule = await _client.GetFromJsonAsync<JsonElement>($"/api/participants/{bob}/schedule");
        Assert.Equal("Confirmed", schedule[0].GetProperty("status").GetString());

        var workshops = await _client.GetFromJsonAsync<JsonElement>("/api/workshops");
        Assert.Equal(0, workshops[0].GetProperty("remainingCapacity").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync("/api/registrations/9999/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Overlapping_registration_returns_409()
    {
        var a = await CreateWorkshop(capacity: 2, startHour: 9, endHour: 11);
        var b = await CreateWorkshop(capacity: 2, startHour: 10, endHour: 12);
        var p = await CreateParticipant("alice");
        await Register(a, p);

        Assert.Equal(HttpStatusCode.Conflict, (await Register(b, p)).StatusCode);
    }
}
