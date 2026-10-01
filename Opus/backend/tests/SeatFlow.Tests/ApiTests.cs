using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Endpoints;

namespace SeatFlow.Tests;

/// <summary>HTTP-level behaviour: status codes, validation, and the Idempotency-Key header.</summary>
public sealed class ApiTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly TestDatabase _db = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:SeatFlow", _db.ConnectionString);
            b.UseSetting("Seed:Enabled", "false");
        });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _db.Dispose();
    }

    private async Task<WorkshopDto> CreateWorkshop(int capacity, string start = "2026-11-12T09:00:00Z", string end = "2026-11-12T10:00:00Z")
    {
        var response = await _client.PostAsJsonAsync("/api/workshops", new { title = "Workshop", startTime = start, endTime = end, capacity });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<WorkshopDto>(Json))!;
    }

    private Task<HttpResponseMessage> Register(int workshopId, int participantId, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/workshops/{workshopId}/registrations")
        {
            Content = JsonContent.Create(new { participantId }),
        };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return _client.SendAsync(request);
    }

    private static async Task<RegistrationDto> Read(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<RegistrationDto>(Json))!;

    [Fact]
    public async Task Register_returns_201_and_workshop_list_reflects_remaining_capacity()
    {
        var workshop = await CreateWorkshop(capacity: 2);
        var ada = await _db.AddParticipant("Ada");

        var response = await Register(workshop.Id, ada.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(RegistrationStatus.Confirmed, (await Read(response)).Status);
        var workshops = await _client.GetFromJsonAsync<List<WorkshopDto>>("/api/workshops", Json);
        var listed = Assert.Single(workshops!);
        Assert.Equal(1, listed.ConfirmedCount);
        Assert.Equal(1, listed.RemainingCapacity);
    }

    [Fact]
    public async Task Idempotency_key_replay_returns_same_registration_and_flags_replay()
    {
        var workshop = await CreateWorkshop(capacity: 2);
        var ada = await _db.AddParticipant("Ada");

        var first = await Register(workshop.Id, ada.Id, key: "k-1");
        var second = await Register(workshop.Id, ada.Id, key: "k-1");

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal((await Read(first)).Id, (await Read(second)).Id);
        Assert.True(second.Headers.Contains("Idempotent-Replayed"));
        var registrations = await _client.GetFromJsonAsync<List<RegistrationDto>>($"/api/workshops/{workshop.Id}/registrations", Json);
        Assert.Single(registrations!);
    }

    [Fact]
    public async Task Without_idempotency_key_a_repeat_is_a_409_duplicate()
    {
        var workshop = await CreateWorkshop(capacity: 2);
        var ada = await _db.AddParticipant("Ada");
        await Register(workshop.Id, ada.Id);

        var repeat = await Register(workshop.Id, ada.Id);

        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
    }

    [Fact]
    public async Task Idempotency_key_reused_for_other_participant_is_422()
    {
        var workshop = await CreateWorkshop(capacity: 2);
        var (ada, bob) = (await _db.AddParticipant("Ada"), await _db.AddParticipant("Bob"));
        await Register(workshop.Id, ada.Id, key: "k-1");

        var response = await Register(workshop.Id, bob.Id, key: "k-1");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Overlapping_confirmed_registration_is_409()
    {
        var a = await CreateWorkshop(5, "2026-11-12T09:00:00Z", "2026-11-12T11:00:00Z");
        var b = await CreateWorkshop(5, "2026-11-12T10:00:00Z", "2026-11-12T12:00:00Z");
        var ada = await _db.AddParticipant("Ada");
        await Register(a.Id, ada.Id);

        var response = await Register(b.Id, ada.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("ScheduleConflict", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cancel_twice_returns_200_both_times()
    {
        var workshop = await CreateWorkshop(capacity: 1);
        var ada = await _db.AddParticipant("Ada");
        var reg = await Read(await Register(workshop.Id, ada.Id));

        var first = await _client.PostAsync($"/api/registrations/{reg.Id}/cancel", null);
        var second = await _client.PostAsync($"/api/registrations/{reg.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<CancelResponse>(Json);
        Assert.True(body!.AlreadyCancelled);
        Assert.Equal(RegistrationStatus.Cancelled, body.Registration.Status);
    }

    [Fact]
    public async Task Waitlist_and_schedule_endpoints_expose_states_and_positions()
    {
        var workshop = await CreateWorkshop(capacity: 1);
        var p = await _db.AddParticipants(3);
        foreach (var participant in p) await Register(workshop.Id, participant.Id);

        var registrations = await _client.GetFromJsonAsync<List<RegistrationDto>>($"/api/workshops/{workshop.Id}/registrations", Json);
        Assert.Equal([null, 1, 2], registrations!.Select(r => r.WaitlistPosition));

        var schedule = await _client.GetFromJsonAsync<ScheduleDto>($"/api/participants/{p[2].Id}/schedule", Json);
        var item = Assert.Single(schedule!.Items);
        Assert.Equal(RegistrationStatus.Waitlisted, item.Status);
        Assert.Equal(2, item.WaitlistPosition);
    }

    [Theory]
    [InlineData("/api/workshops/999/registrations", "GET")]
    [InlineData("/api/participants/999/schedule", "GET")]
    [InlineData("/api/registrations/999/cancel", "POST")]
    public async Task Unknown_ids_are_404(string url, string method)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Registering_unknown_participant_or_workshop_is_404()
    {
        var workshop = await CreateWorkshop(capacity: 1);
        var ada = await _db.AddParticipant("Ada");

        Assert.Equal(HttpStatusCode.NotFound, (await Register(workshop.Id, 999)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Register(999, ada.Id)).StatusCode);
    }

    [Fact]
    public async Task Invalid_input_is_400_validation_problem()
    {
        var badWorkshop = await _client.PostAsJsonAsync("/api/workshops",
            new { title = "", startTime = "2026-11-12T10:00:00Z", endTime = "2026-11-12T09:00:00Z", capacity = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, badWorkshop.StatusCode);
        var problem = await badWorkshop.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("endTime", out _));
        Assert.True(errors.TryGetProperty("capacity", out _));

        var workshop = await CreateWorkshop(capacity: 1);
        var badRegistration = await _client.PostAsJsonAsync($"/api/workshops/{workshop.Id}/registrations", new { });
        Assert.Equal(HttpStatusCode.BadRequest, badRegistration.StatusCode);
    }

    [Fact]
    public async Task Concurrent_http_requests_for_the_last_seat_confirm_only_one()
    {
        var workshop = await CreateWorkshop(capacity: 1);
        var participants = await _db.AddParticipants(10);

        var responses = await Task.WhenAll(participants.Select(p => Register(workshop.Id, p.Id)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var statuses = await Task.WhenAll(responses.Select(async r => (await Read(r)).Status));
        Assert.Single(statuses, s => s == RegistrationStatus.Confirmed);
        Assert.Equal(9, statuses.Count(s => s == RegistrationStatus.Waitlisted));
    }
}
