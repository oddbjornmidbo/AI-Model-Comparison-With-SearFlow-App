using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using SeatFlow.Api.Api;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Notifications;

namespace SeatFlow.Tests;

public sealed class RegistrationTests : IDisposable
{
    private readonly SeatFlowApp _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Registering_into_workshop_with_free_seats_confirms_and_notifies()
    {
        var workshop = await _app.CreateWorkshopAsync("DDD", capacity: 2);
        var ada = await _app.CreateParticipantAsync("Ada");

        var registration = await _app.RegisterAsync(workshop.Id, ada.Id);

        Assert.Equal(RegistrationStatus.Confirmed, registration.Status);
        Assert.Null(registration.WaitlistPosition);
        var updated = await _app.GetWorkshopAsync(workshop.Id);
        Assert.Equal(1, updated.ConfirmedCount);
        Assert.Equal(1, updated.RemainingSeats);
        var notification = Assert.Single(_app.Notifications.Sent);
        Assert.Equal(NotificationType.RegistrationConfirmed, notification.Type);
        Assert.Equal(registration.Id, notification.RegistrationId);
    }

    [Fact]
    public async Task Registering_into_full_workshop_waitlists_in_arrival_order()
    {
        var workshop = await _app.CreateWorkshopAsync("DDD", capacity: 1);
        var ada = await _app.CreateParticipantAsync("Ada");
        var grace = await _app.CreateParticipantAsync("Grace");
        var alan = await _app.CreateParticipantAsync("Alan");
        await _app.RegisterAsync(workshop.Id, ada.Id);

        var second = await _app.RegisterAsync(workshop.Id, grace.Id);
        var third = await _app.RegisterAsync(workshop.Id, alan.Id);

        Assert.Equal(RegistrationStatus.Waitlisted, second.Status);
        Assert.Equal(1, second.WaitlistPosition);
        Assert.Equal(RegistrationStatus.Waitlisted, third.Status);
        Assert.Equal(2, third.WaitlistPosition);
        var updated = await _app.GetWorkshopAsync(workshop.Id);
        Assert.Equal(0, updated.RemainingSeats);
        Assert.Equal(2, updated.WaitlistedCount);
        // Only the confirmation is notified; waitlisting is not one of the notification triggers.
        Assert.Single(_app.Notifications.Sent);
    }

    [Fact]
    public async Task Cancelling_a_confirmed_seat_promotes_the_oldest_waitlisted_registration()
    {
        var workshop = await _app.CreateWorkshopAsync("DDD", capacity: 1);
        var ada = await _app.CreateParticipantAsync("Ada");
        var grace = await _app.CreateParticipantAsync("Grace");
        var alan = await _app.CreateParticipantAsync("Alan");
        var confirmed = await _app.RegisterAsync(workshop.Id, ada.Id);
        var first = await _app.RegisterAsync(workshop.Id, grace.Id);
        var second = await _app.RegisterAsync(workshop.Id, alan.Id);

        var cancelled = await _app.CancelAsync(confirmed.Id);

        Assert.Equal(RegistrationStatus.Cancelled, cancelled.Status);
        var registrations = (await _app.GetRegistrationsAsync(workshop.Id)).ToDictionary(r => r.Id);
        Assert.Equal(RegistrationStatus.Confirmed, registrations[first.Id].Status);
        Assert.Equal(RegistrationStatus.Waitlisted, registrations[second.Id].Status);
        Assert.Equal(1, registrations[second.Id].WaitlistPosition);
        Assert.Contains(_app.Notifications.Sent, n => n.Type == NotificationType.RegistrationCancelled && n.RegistrationId == confirmed.Id);
        Assert.Contains(_app.Notifications.Sent, n => n.Type == NotificationType.PromotedFromWaitlist && n.RegistrationId == first.Id);
    }

    [Fact]
    public async Task Promotion_skips_waitlisted_participant_with_schedule_conflict_and_keeps_them_waitlisted()
    {
        var morning = await _app.CreateWorkshopAsync("Event Sourcing", capacity: 5, startHour: 9, durationHours: 2);
        var overlapping = await _app.CreateWorkshopAsync("DDD", capacity: 1, startHour: 10, durationHours: 2);
        var ada = await _app.CreateParticipantAsync("Ada");
        var busyAlan = await _app.CreateParticipantAsync("Alan");
        var grace = await _app.CreateParticipantAsync("Grace");
        await _app.RegisterAsync(morning.Id, busyAlan.Id);
        var adaSeat = await _app.RegisterAsync(overlapping.Id, ada.Id);
        var alanWaiting = await _app.RegisterAsync(overlapping.Id, busyAlan.Id);
        var graceWaiting = await _app.RegisterAsync(overlapping.Id, grace.Id);
        Assert.Equal(RegistrationStatus.Waitlisted, alanWaiting.Status);

        await _app.CancelAsync(adaSeat.Id);

        var registrations = (await _app.GetRegistrationsAsync(overlapping.Id)).ToDictionary(r => r.Id);
        Assert.Equal(RegistrationStatus.Waitlisted, registrations[alanWaiting.Id].Status);
        Assert.Equal(1, registrations[alanWaiting.Id].WaitlistPosition);
        Assert.Equal(RegistrationStatus.Confirmed, registrations[graceWaiting.Id].Status);
        Assert.DoesNotContain(_app.Notifications.Sent, n => n.Type == NotificationType.PromotedFromWaitlist && n.RegistrationId == alanWaiting.Id);
    }

    [Fact]
    public async Task Seat_left_free_by_conflict_is_given_to_the_participant_once_their_conflict_is_cancelled()
    {
        var morning = await _app.CreateWorkshopAsync("Event Sourcing", capacity: 5, startHour: 9, durationHours: 2);
        var overlapping = await _app.CreateWorkshopAsync("DDD", capacity: 1, startHour: 10, durationHours: 2);
        var ada = await _app.CreateParticipantAsync("Ada");
        var alan = await _app.CreateParticipantAsync("Alan");
        var alanMorning = await _app.RegisterAsync(morning.Id, alan.Id);
        var adaSeat = await _app.RegisterAsync(overlapping.Id, ada.Id);
        var alanWaiting = await _app.RegisterAsync(overlapping.Id, alan.Id);
        await _app.CancelAsync(adaSeat.Id); // nobody eligible: the seat stays free
        Assert.Equal(1, (await _app.GetWorkshopAsync(overlapping.Id)).RemainingSeats);

        await _app.CancelAsync(alanMorning.Id);

        var schedule = await _app.GetScheduleAsync(alan.Id);
        Assert.Equal(RegistrationStatus.Confirmed, schedule.Registrations.Single(r => r.RegistrationId == alanWaiting.Id).Status);
    }

    [Fact]
    public async Task Registering_for_an_overlapping_workshop_with_free_seats_is_rejected()
    {
        var morning = await _app.CreateWorkshopAsync("Event Sourcing", capacity: 5, startHour: 9, durationHours: 2);
        var overlapping = await _app.CreateWorkshopAsync("DDD", capacity: 5, startHour: 10, durationHours: 2);
        var adjacent = await _app.CreateWorkshopAsync("Testing", capacity: 5, startHour: 11, durationHours: 1);
        var ada = await _app.CreateParticipantAsync("Ada");
        await _app.RegisterAsync(morning.Id, ada.Id);

        var response = await _app.PostRegistrationAsync(overlapping.Id, ada.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("schedule_conflict", await ProblemCodeAsync(response));
        Assert.Empty(await _app.GetRegistrationsAsync(overlapping.Id));
        // Back-to-back sessions (11:00 end / 11:00 start) do not overlap.
        Assert.Equal(RegistrationStatus.Confirmed, (await _app.RegisterAsync(adjacent.Id, ada.Id)).Status);
    }

    [Fact]
    public async Task Registering_twice_for_same_workshop_is_rejected_unless_previous_was_cancelled()
    {
        var workshop = await _app.CreateWorkshopAsync("DDD", capacity: 5);
        var ada = await _app.CreateParticipantAsync("Ada");
        var first = await _app.RegisterAsync(workshop.Id, ada.Id);

        var duplicate = await _app.PostRegistrationAsync(workshop.Id, ada.Id);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("duplicate_registration", await ProblemCodeAsync(duplicate));

        await _app.CancelAsync(first.Id);
        var again = await _app.RegisterAsync(workshop.Id, ada.Id);
        Assert.NotEqual(first.Id, again.Id);
        Assert.Equal(RegistrationStatus.Confirmed, again.Status);
    }

    [Fact]
    public async Task Waitlisted_participant_cannot_register_again_for_the_same_workshop()
    {
        var workshop = await _app.CreateWorkshopAsync("DDD", capacity: 1);
        var ada = await _app.CreateParticipantAsync("Ada");
        var grace = await _app.CreateParticipantAsync("Grace");
        await _app.RegisterAsync(workshop.Id, ada.Id);
        await _app.RegisterAsync(workshop.Id, grace.Id);

        var duplicate = await _app.PostRegistrationAsync(workshop.Id, grace.Id);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Repeating_a_request_with_the_same_idempotency_key_returns_the_same_registration()
    {
        var workshop = await _app.CreateWorkshopAsync("DDD", capacity: 5);
        var ada = await _app.CreateParticipantAsync("Ada");
        var key = Guid.NewGuid().ToString();

        var first = await _app.PostRegistrationAsync(workshop.Id, ada.Id, key);
        var second = await _app.PostRegistrationAsync(workshop.Id, ada.Id, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.True(second.Headers.Contains("Idempotent-Replayed"));
        var firstBody = await first.Content.ReadFromJsonAsync<RegistrationDto>(SeatFlowApp.Json);
        var secondBody = await second.Content.ReadFromJsonAsync<RegistrationDto>(SeatFlowApp.Json);
        Assert.Equal(firstBody, secondBody);
        Assert.Single(await _app.GetRegistrationsAsync(workshop.Id));
        Assert.Single(_app.Notifications.Sent);
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_idempotency_key_create_one_registration()
    {
        var workshop = await _app.CreateWorkshopAsync("DDD", capacity: 5);
        var ada = await _app.CreateParticipantAsync("Ada");
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => _app.PostRegistrationAsync(workshop.Id, ada.Id, key)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var ids = await Task.WhenAll(responses.Select(async r => (await r.Content.ReadFromJsonAsync<RegistrationDto>(SeatFlowApp.Json))!.Id));
        Assert.Single(ids.Distinct());
        Assert.Single(await _app.GetRegistrationsAsync(workshop.Id));
    }

    [Fact]
    public async Task Reusing_an_idempotency_key_for_a_different_request_is_rejected()
    {
        var workshop = await _app.CreateWorkshopAsync("DDD", capacity: 5);
        var ada = await _app.CreateParticipantAsync("Ada");
        var grace = await _app.CreateParticipantAsync("Grace");
        var key = Guid.NewGuid().ToString();
        await _app.PostRegistrationAsync(workshop.Id, ada.Id, key);

        var response = await _app.PostRegistrationAsync(workshop.Id, grace.Id, key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("idempotency_key_reused", await ProblemCodeAsync(response));
        Assert.Single(await _app.GetRegistrationsAsync(workshop.Id));
    }

    [Fact]
    public async Task Cancelling_twice_is_idempotent_and_does_not_promote_twice()
    {
        var workshop = await _app.CreateWorkshopAsync("DDD", capacity: 1);
        var ada = await _app.CreateParticipantAsync("Ada");
        var grace = await _app.CreateParticipantAsync("Grace");
        var alan = await _app.CreateParticipantAsync("Alan");
        var adaSeat = await _app.RegisterAsync(workshop.Id, ada.Id);
        await _app.RegisterAsync(workshop.Id, grace.Id);
        var alanWaiting = await _app.RegisterAsync(workshop.Id, alan.Id);

        var first = await _app.CancelAsync(adaSeat.Id);
        var second = await _app.CancelAsync(adaSeat.Id);

        Assert.Equal(RegistrationStatus.Cancelled, first.Status);
        Assert.Equal(first, second);
        var registrations = (await _app.GetRegistrationsAsync(workshop.Id)).ToDictionary(r => r.Id);
        Assert.Equal(RegistrationStatus.Waitlisted, registrations[alanWaiting.Id].Status);
        Assert.Equal(1, registrations.Values.Count(r => r.Status == RegistrationStatus.Confirmed));
        Assert.Single(_app.Notifications.Sent, n => n.Type == NotificationType.RegistrationCancelled);
    }

    [Fact]
    public async Task Concurrent_attempts_for_the_final_seat_confirm_exactly_one()
    {
        var workshop = await _app.CreateWorkshopAsync("DDD", capacity: 3);
        var early = await Task.WhenAll(Enumerable.Range(0, 2).Select(i => _app.CreateParticipantAsync($"Early {i}")));
        foreach (var p in early) await _app.RegisterAsync(workshop.Id, p.Id);
        var racers = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => _app.CreateParticipantAsync($"Racer {i}")));

        var responses = await Task.WhenAll(racers.Select(p => _app.PostRegistrationAsync(workshop.Id, p.Id)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var results = await Task.WhenAll(responses.Select(async r => (await r.Content.ReadFromJsonAsync<RegistrationDto>(SeatFlowApp.Json))!));
        Assert.Single(results, r => r.Status == RegistrationStatus.Confirmed);
        Assert.Equal(19, results.Count(r => r.Status == RegistrationStatus.Waitlisted));
        Assert.Equal(Enumerable.Range(1, 19), results.Where(r => r.WaitlistPosition is not null).Select(r => r.WaitlistPosition!.Value).Order());
        var updated = await _app.GetWorkshopAsync(workshop.Id);
        Assert.Equal(3, updated.ConfirmedCount);
    }

    [Fact]
    public async Task Concurrent_registrations_for_overlapping_workshops_confirm_at_most_one()
    {
        var a = await _app.CreateWorkshopAsync("Event Sourcing", capacity: 5, startHour: 9, durationHours: 2);
        var b = await _app.CreateWorkshopAsync("DDD", capacity: 5, startHour: 10, durationHours: 2);
        var ada = await _app.CreateParticipantAsync("Ada");

        var responses = await Task.WhenAll(_app.PostRegistrationAsync(a.Id, ada.Id), _app.PostRegistrationAsync(b.Id, ada.Id));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var schedule = await _app.GetScheduleAsync(ada.Id);
        Assert.Single(schedule.Registrations, r => r.Status == RegistrationStatus.Confirmed);
    }

    [Fact]
    public async Task Notification_failures_do_not_roll_back_registration_or_cancellation()
    {
        var workshop = await _app.CreateWorkshopAsync("DDD", capacity: 1);
        var ada = await _app.CreateParticipantAsync("Ada");
        var grace = await _app.CreateParticipantAsync("Grace");
        _app.Notifications.Fail = true;

        var registration = await _app.RegisterAsync(workshop.Id, ada.Id);
        var waiting = await _app.RegisterAsync(workshop.Id, grace.Id);
        var cancelled = await _app.CancelAsync(registration.Id);

        Assert.Equal(RegistrationStatus.Cancelled, cancelled.Status);
        var registrations = (await _app.GetRegistrationsAsync(workshop.Id)).ToDictionary(r => r.Id);
        Assert.Equal(RegistrationStatus.Cancelled, registrations[registration.Id].Status);
        Assert.Equal(RegistrationStatus.Confirmed, registrations[waiting.Id].Status);
    }

    [Theory]
    [InlineData(999, true)]
    [InlineData(1, false)]
    public async Task Registering_with_unknown_workshop_or_participant_returns_404(int workshopId, bool unknownWorkshop)
    {
        var workshop = await _app.CreateWorkshopAsync("DDD", capacity: 1);
        var ada = await _app.CreateParticipantAsync("Ada");

        var response = unknownWorkshop
            ? await _app.PostRegistrationAsync(workshopId, ada.Id)
            : await _app.PostRegistrationAsync(workshop.Id, 999);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_requests_return_validation_problems()
    {
        var client = _app.CreateClient();

        var badWorkshop = await client.PostAsJsonAsync("/api/workshops", new
        {
            title = "",
            startTime = DateTimeOffset.UtcNow,
            endTime = DateTimeOffset.UtcNow.AddHours(-1),
            capacity = 0,
        });
        var badRegistration = await client.PostAsJsonAsync("/api/workshops/1/registrations", new { });

        Assert.Equal(HttpStatusCode.BadRequest, badWorkshop.StatusCode);
        var problem = await badWorkshop.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["capacity", "endTime", "title"], problem!.Errors.Keys.Order());
        Assert.Equal(HttpStatusCode.BadRequest, badRegistration.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/registrations/999/cancel", null)).StatusCode);
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        return problem?.Extensions.TryGetValue("code", out var code) == true ? code?.ToString() : null;
    }
}
