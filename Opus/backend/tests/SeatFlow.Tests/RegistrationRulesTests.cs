using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Notifications;
using SeatFlow.Api.Services;

namespace SeatFlow.Tests;

public sealed class RegistrationRulesTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Registering_into_workshop_with_free_seats_is_confirmed_and_notified()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 2);
        var ada = await _db.AddParticipant("Ada");

        var result = await _db.Register(workshop, ada);

        Assert.Equal(RegistrationStatus.Confirmed, result.Registration.Status);
        Assert.False(result.Replayed);
        var n = Assert.Single(_db.Notifications.Sent);
        Assert.Equal(NotificationType.RegistrationConfirmed, n.Type);
        Assert.Equal(result.Registration.Id, n.RegistrationId);
    }

    [Fact]
    public async Task Registering_into_full_workshop_is_waitlisted_without_confirmation_notice()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 1);
        var (p1, p2) = (await _db.AddParticipant("One"), await _db.AddParticipant("Two"));

        await _db.Register(workshop, p1);
        var second = await _db.Register(workshop, p2);

        Assert.Equal(RegistrationStatus.Waitlisted, second.Registration.Status);
        Assert.DoesNotContain(_db.Notifications.Sent, n => n.RegistrationId == second.Registration.Id);
    }

    [Fact]
    public async Task Cancelling_a_confirmed_seat_promotes_the_oldest_waitlisted_registration()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 1);
        var p = await _db.AddParticipants(4);

        var holder = await _db.Register(workshop, p[0]);
        var first = await _db.Register(workshop, p[1]);
        var second = await _db.Register(workshop, p[2]);
        var third = await _db.Register(workshop, p[3]);

        var cancel = await _db.Cancel(holder.Registration.Id);

        Assert.Equal(first.Registration.Id, Assert.Single(cancel.Promoted).Id);
        Assert.Equal(RegistrationStatus.Confirmed, await _db.StatusOf(first.Registration.Id));
        Assert.Equal(RegistrationStatus.Waitlisted, await _db.StatusOf(second.Registration.Id));
        Assert.Equal(RegistrationStatus.Waitlisted, await _db.StatusOf(third.Registration.Id));

        // FIFO continues on the next cancellation.
        var next = await _db.Cancel(first.Registration.Id);
        Assert.Equal(second.Registration.Id, Assert.Single(next.Promoted).Id);

        Assert.Contains(_db.Notifications.Sent, n =>
            n.Type == NotificationType.RegistrationCancelled && n.RegistrationId == holder.Registration.Id);
        Assert.Contains(_db.Notifications.Sent, n =>
            n.Type == NotificationType.PromotedFromWaitlist && n.RegistrationId == first.Registration.Id);
    }

    [Fact]
    public async Task Promotion_skips_waitlisted_participant_with_schedule_conflict_and_keeps_them_waitlisted()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "11:00", capacity: 1);
        var overlapping = await _db.AddWorkshop("Async", "10:30", "12:00", capacity: 5);
        var (holder, busy, free) = (await _db.AddParticipant("Holder"), await _db.AddParticipant("Busy"), await _db.AddParticipant("Free"));

        var held = await _db.Register(workshop, holder);
        var busyWait = await _db.Register(workshop, busy);   // first in line...
        var freeWait = await _db.Register(workshop, free);
        await _db.Register(overlapping, busy);               // ...but then confirms an overlapping workshop

        var cancel = await _db.Cancel(held.Registration.Id);

        Assert.Equal(freeWait.Registration.Id, Assert.Single(cancel.Promoted).Id);
        Assert.Equal(RegistrationStatus.Confirmed, await _db.StatusOf(freeWait.Registration.Id));
        Assert.Equal(RegistrationStatus.Waitlisted, await _db.StatusOf(busyWait.Registration.Id));
    }

    [Fact]
    public async Task Seat_stays_free_when_every_waitlisted_participant_has_a_conflict()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "11:00", capacity: 1);
        var overlapping = await _db.AddWorkshop("Async", "10:00", "12:00", capacity: 5);
        var (holder, busy) = (await _db.AddParticipant("Holder"), await _db.AddParticipant("Busy"));

        var held = await _db.Register(workshop, holder);
        var waiting = await _db.Register(workshop, busy);
        await _db.Register(overlapping, busy);

        var cancel = await _db.Cancel(held.Registration.Id);

        Assert.Empty(cancel.Promoted);
        Assert.Equal(RegistrationStatus.Waitlisted, await _db.StatusOf(waiting.Registration.Id));
    }

    [Fact]
    public async Task Skipped_participant_is_promoted_once_they_cancel_their_conflicting_workshop()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "11:00", capacity: 1);
        var overlapping = await _db.AddWorkshop("Async", "10:00", "12:00", capacity: 5);
        var (holder, busy) = (await _db.AddParticipant("Holder"), await _db.AddParticipant("Busy"));

        var held = await _db.Register(workshop, holder);
        var waiting = await _db.Register(workshop, busy);
        var conflicting = await _db.Register(overlapping, busy);
        await _db.Cancel(held.Registration.Id); // seat left empty: Busy has a conflict

        var cancel = await _db.Cancel(conflicting.Registration.Id);

        Assert.Equal(waiting.Registration.Id, Assert.Single(cancel.Promoted).Id);
        Assert.Equal(RegistrationStatus.Confirmed, await _db.StatusOf(waiting.Registration.Id));
    }

    [Fact]
    public async Task Confirming_an_overlapping_workshop_is_rejected()
    {
        var first = await _db.AddWorkshop("EF", "09:00", "11:00", capacity: 5);
        var overlapping = await _db.AddWorkshop("Async", "10:30", "12:00", capacity: 5);
        var ada = await _db.AddParticipant("Ada");
        await _db.Register(first, ada);

        var ex = await Assert.ThrowsAsync<DomainException>(() => _db.Register(overlapping, ada));

        Assert.Equal(DomainError.ScheduleConflict, ex.Error);
        await using var db = _db.CreateContext();
        Assert.Equal(1, await db.Registrations.CountAsync(r => r.ParticipantId == ada.Id));
    }

    [Fact]
    public async Task Back_to_back_workshops_do_not_conflict()
    {
        var morning = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 5);
        var next = await _db.AddWorkshop("Async", "10:00", "11:00", capacity: 5);
        var ada = await _db.AddParticipant("Ada");

        await _db.Register(morning, ada);
        var result = await _db.Register(next, ada);

        Assert.Equal(RegistrationStatus.Confirmed, result.Registration.Status);
    }

    [Fact]
    public async Task Overlapping_workshop_that_is_full_can_still_be_waitlisted()
    {
        var first = await _db.AddWorkshop("EF", "09:00", "11:00", capacity: 5);
        var overlapping = await _db.AddWorkshop("Async", "10:30", "12:00", capacity: 1);
        var (ada, bob) = (await _db.AddParticipant("Ada"), await _db.AddParticipant("Bob"));
        await _db.Register(first, ada);
        await _db.Register(overlapping, bob);

        var result = await _db.Register(overlapping, ada);

        Assert.Equal(RegistrationStatus.Waitlisted, result.Registration.Status);
    }

    [Theory]
    [InlineData(false)] // existing registration is Confirmed
    [InlineData(true)]  // existing registration is Waitlisted
    public async Task Registering_twice_for_the_same_workshop_is_rejected(bool whileWaitlisted)
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 1);
        var ada = await _db.AddParticipant("Ada");
        if (whileWaitlisted)
            await _db.Register(workshop, await _db.AddParticipant("Filler"));
        var existing = await _db.Register(workshop, ada);
        Assert.Equal(whileWaitlisted ? RegistrationStatus.Waitlisted : RegistrationStatus.Confirmed, existing.Registration.Status);

        var ex = await Assert.ThrowsAsync<DomainException>(() => _db.Register(workshop, ada));

        Assert.Equal(DomainError.AlreadyRegistered, ex.Error);
    }

    [Fact]
    public async Task Participant_may_register_again_after_cancelling()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 1);
        var ada = await _db.AddParticipant("Ada");
        var first = await _db.Register(workshop, ada);
        await _db.Cancel(first.Registration.Id);

        var again = await _db.Register(workshop, ada);

        Assert.NotEqual(first.Registration.Id, again.Registration.Id);
        Assert.Equal(RegistrationStatus.Confirmed, again.Registration.Status);
    }

    [Fact]
    public async Task Same_idempotency_key_returns_the_same_registration_without_duplicating()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 5);
        var ada = await _db.AddParticipant("Ada");

        var first = await _db.Register(workshop, ada, key: "abc-123");
        var replay = await _db.Register(workshop, ada, key: "abc-123");

        Assert.False(first.Replayed);
        Assert.True(replay.Replayed);
        Assert.Equal(first.Registration.Id, replay.Registration.Id);
        Assert.Equal(first.Registration.Status, replay.Registration.Status);
        Assert.Single(_db.Notifications.Sent); // no second confirmation notice
        await using var db = _db.CreateContext();
        Assert.Equal(1, await db.Registrations.CountAsync());
    }

    [Fact]
    public async Task Reusing_an_idempotency_key_for_a_different_request_is_rejected()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 5);
        var (ada, bob) = (await _db.AddParticipant("Ada"), await _db.AddParticipant("Bob"));
        await _db.Register(workshop, ada, key: "shared");

        var ex = await Assert.ThrowsAsync<DomainException>(() => _db.Register(workshop, bob, key: "shared"));

        Assert.Equal(DomainError.IdempotencyKeyReused, ex.Error);
    }

    [Fact]
    public async Task Cancelling_twice_is_idempotent_and_does_not_promote_twice()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 1);
        var p = await _db.AddParticipants(3);
        var holder = await _db.Register(workshop, p[0]);
        await _db.Register(workshop, p[1]);
        var stillWaiting = await _db.Register(workshop, p[2]);

        var first = await _db.Cancel(holder.Registration.Id);
        var notificationsAfterFirst = _db.Notifications.Sent.Count;
        var second = await _db.Cancel(holder.Registration.Id);

        Assert.False(first.AlreadyCancelled);
        Assert.True(second.AlreadyCancelled);
        Assert.Equal(RegistrationStatus.Cancelled, second.Registration.Status);
        Assert.Empty(second.Promoted);
        Assert.Equal(first.Registration.CancelledAt, second.Registration.CancelledAt);
        Assert.Equal(notificationsAfterFirst, _db.Notifications.Sent.Count);
        Assert.Equal(RegistrationStatus.Waitlisted, await _db.StatusOf(stillWaiting.Registration.Id));
    }

    [Fact]
    public async Task Cancelling_a_waitlisted_registration_does_not_promote_anyone()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 1);
        var p = await _db.AddParticipants(3);
        await _db.Register(workshop, p[0]);
        var waiting = await _db.Register(workshop, p[1]);
        var behind = await _db.Register(workshop, p[2]);

        var cancel = await _db.Cancel(waiting.Registration.Id);

        Assert.Empty(cancel.Promoted);
        Assert.Equal(RegistrationStatus.Waitlisted, await _db.StatusOf(behind.Registration.Id));
    }

    [Fact]
    public async Task Notification_failures_do_not_roll_back_registration_or_cancellation()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 1);
        var (ada, bob) = (await _db.AddParticipant("Ada"), await _db.AddParticipant("Bob"));
        var failing = new FailingNotificationService();

        RegisterResult held, waiting;
        CancelResult cancel;
        await using (var db = _db.CreateContext())
        {
            var service = _db.CreateService(db, failing);
            held = await service.RegisterAsync(workshop.Id, ada.Id, null);
            waiting = await service.RegisterAsync(workshop.Id, bob.Id, null);
            cancel = await service.CancelAsync(held.Registration.Id);
        }

        Assert.Equal(RegistrationStatus.Cancelled, await _db.StatusOf(held.Registration.Id));
        Assert.Equal(RegistrationStatus.Confirmed, await _db.StatusOf(waiting.Registration.Id));
        Assert.Single(cancel.Promoted);
    }
}
