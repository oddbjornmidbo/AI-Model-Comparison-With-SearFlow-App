using SeatFlow.Domain;
using SeatFlow.Services;

namespace SeatFlow.Tests;

public class RegistrationServiceTests : IDisposable
{
    private readonly TestApp _app = new();
    public void Dispose() => _app.Dispose();

    private async Task<Registration> Register(int workshop, int participant, string? key = null) =>
        (await _app.NewService().RegisterAsync(workshop, participant, key)).Registration;

    [Fact]
    public async Task Registering_into_workshop_with_capacity_is_confirmed_and_notifies()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 2);
        var alice = _app.AddParticipant("Alice");

        var reg = await Register(w, alice);

        Assert.Equal(RegistrationStatus.Confirmed, reg.Status);
        Assert.Contains(_app.Notifier.Sent, n => n.Type == NotificationType.RegistrationConfirmed && n.ParticipantId == alice);
    }

    [Fact]
    public async Task Registering_into_full_workshop_is_waitlisted()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 1);
        await Register(w, _app.AddParticipant("Alice"));

        var reg = await Register(w, _app.AddParticipant("Bob"));

        Assert.Equal(RegistrationStatus.Waitlisted, reg.Status);
        Assert.DoesNotContain(_app.Notifier.Sent, n => n.ParticipantId == reg.ParticipantId);
    }

    [Fact]
    public async Task Cancelling_a_confirmed_registration_promotes_oldest_waitlisted_first()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 1);
        var first = await Register(w, _app.AddParticipant("Alice"));
        var second = await Register(w, _app.AddParticipant("Bob"));
        var third = await Register(w, _app.AddParticipant("Carol"));

        await _app.NewService().CancelAsync(first.Id);

        Assert.Equal(RegistrationStatus.Cancelled, _app.StatusOf(first.Id));
        Assert.Equal(RegistrationStatus.Confirmed, _app.StatusOf(second.Id));
        Assert.Equal(RegistrationStatus.Waitlisted, _app.StatusOf(third.Id));
        Assert.Contains(_app.Notifier.Sent, n => n.Type == NotificationType.WaitlistPromoted && n.ParticipantId == second.ParticipantId);
        Assert.Contains(_app.Notifier.Sent, n => n.Type == NotificationType.RegistrationCancelled && n.ParticipantId == first.ParticipantId);
    }

    [Fact]
    public async Task Promotion_skips_waitlisted_participant_with_schedule_conflict_and_leaves_them_waitlisted()
    {
        var full = _app.AddWorkshop("Full", 9, 11, capacity: 1);
        var overlapping = _app.AddWorkshop("Overlapping", 10, 12, capacity: 5);
        var holder = _app.AddParticipant("Holder");
        var conflicted = _app.AddParticipant("Conflicted");
        var eligible = _app.AddParticipant("Eligible");

        var holderReg = await Register(full, holder);
        var conflictedWaitlist = await Register(full, conflicted);   // waitlisted first
        var eligibleWaitlist = await Register(full, eligible);       // waitlisted second
        // The first waitlisted participant later gets a confirmed seat in an overlapping workshop.
        await Register(overlapping, conflicted);

        await _app.NewService().CancelAsync(holderReg.Id);

        Assert.Equal(RegistrationStatus.Waitlisted, _app.StatusOf(conflictedWaitlist.Id));
        Assert.Equal(RegistrationStatus.Confirmed, _app.StatusOf(eligibleWaitlist.Id));
    }

    [Fact]
    public async Task Seat_stays_open_for_newcomers_when_the_only_waitlisted_participant_is_conflicted()
    {
        var full = _app.AddWorkshop("Full", 9, 11, capacity: 1);
        var overlapping = _app.AddWorkshop("Overlapping", 10, 12, capacity: 5);
        var holder = await Register(full, _app.AddParticipant("Holder"));
        var conflicted = _app.AddParticipant("Conflicted");
        var waitlisted = await Register(full, conflicted);
        await Register(overlapping, conflicted);

        await _app.NewService().CancelAsync(holder.Id);             // nobody eligible; seat stays free
        Assert.Equal(RegistrationStatus.Waitlisted, _app.StatusOf(waitlisted.Id));

        // A newcomer can take the free seat.
        var newcomer = await Register(full, _app.AddParticipant("Newcomer"));
        Assert.Equal(RegistrationStatus.Confirmed, newcomer.Status);
    }

    [Fact]
    public async Task Cancelling_a_waitlisted_registration_does_not_promote_anyone()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 1);
        await Register(w, _app.AddParticipant("Alice"));
        var waitlisted = await Register(w, _app.AddParticipant("Bob"));
        var other = await Register(w, _app.AddParticipant("Carol"));

        await _app.NewService().CancelAsync(waitlisted.Id);

        Assert.Equal(RegistrationStatus.Waitlisted, _app.StatusOf(other.Id));
        Assert.DoesNotContain(_app.Notifier.Sent, n => n.Type == NotificationType.WaitlistPromoted);
    }

    [Fact]
    public async Task Overlapping_confirmed_registration_is_rejected()
    {
        var a = _app.AddWorkshop("A", 9, 11, capacity: 5);
        var b = _app.AddWorkshop("B", 10, 12, capacity: 5);
        var alice = _app.AddParticipant("Alice");
        await Register(a, alice);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Register(b, alice));

        Assert.Equal(DomainErrorKind.Conflict, ex.Kind);
    }

    [Fact]
    public async Task Back_to_back_workshops_do_not_overlap()
    {
        var a = _app.AddWorkshop("A", 9, 10, capacity: 5);
        var b = _app.AddWorkshop("B", 10, 11, capacity: 5);
        var alice = _app.AddParticipant("Alice");
        await Register(a, alice);

        Assert.Equal(RegistrationStatus.Confirmed, (await Register(b, alice)).Status);
    }

    [Fact]
    public async Task Cancelled_workshop_no_longer_blocks_overlapping_registration()
    {
        var a = _app.AddWorkshop("A", 9, 11, capacity: 5);
        var b = _app.AddWorkshop("B", 10, 12, capacity: 5);
        var alice = _app.AddParticipant("Alice");
        var regA = await Register(a, alice);
        await _app.NewService().CancelAsync(regA.Id);

        Assert.Equal(RegistrationStatus.Confirmed, (await Register(b, alice)).Status);
    }

    [Fact]
    public async Task Duplicate_active_registration_is_rejected_but_allowed_after_cancellation()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 5);
        var alice = _app.AddParticipant("Alice");
        var first = await Register(w, alice);

        var ex = await Assert.ThrowsAsync<DomainException>(() => Register(w, alice));
        Assert.Equal(DomainErrorKind.Conflict, ex.Kind);

        await _app.NewService().CancelAsync(first.Id);
        var again = await Register(w, alice);
        Assert.NotEqual(first.Id, again.Id);
        Assert.Equal(RegistrationStatus.Confirmed, again.Status);
    }

    [Fact]
    public async Task Duplicate_registration_is_rejected_for_waitlisted_participant_too()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 1);
        await Register(w, _app.AddParticipant("Alice"));
        var bob = _app.AddParticipant("Bob");
        await Register(w, bob);

        await Assert.ThrowsAsync<DomainException>(() => Register(w, bob));
    }

    [Fact]
    public async Task Same_idempotency_key_returns_same_registration_without_duplicates_or_extra_notifications()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 5);
        var alice = _app.AddParticipant("Alice");

        var first = await _app.NewService().RegisterAsync(w, alice, "key-1");
        var second = await _app.NewService().RegisterAsync(w, alice, "key-1");

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Registration.Id, second.Registration.Id);
        using var db = _app.NewDb();
        Assert.Single(db.Registrations);
        Assert.Single(_app.Notifier.Sent);
    }

    [Fact]
    public async Task Idempotency_key_replay_still_works_after_registration_was_cancelled()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 5);
        var alice = _app.AddParticipant("Alice");
        var first = await _app.NewService().RegisterAsync(w, alice, "key-1");
        await _app.NewService().CancelAsync(first.Registration.Id);

        var replay = await _app.NewService().RegisterAsync(w, alice, "key-1");

        Assert.Equal(first.Registration.Id, replay.Registration.Id);
        using var db = _app.NewDb();
        Assert.Single(db.Registrations);
    }

    [Fact]
    public async Task Idempotency_key_reused_for_a_different_request_is_a_conflict()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 5);
        await _app.NewService().RegisterAsync(w, _app.AddParticipant("Alice"), "key-1");

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _app.NewService().RegisterAsync(w, _app.AddParticipant("Bob"), "key-1"));

        Assert.Equal(DomainErrorKind.Conflict, ex.Kind);
    }

    [Fact]
    public async Task Cancelling_twice_is_idempotent_and_does_not_promote_twice_or_renotify()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 1);
        var first = await Register(w, _app.AddParticipant("Alice"));
        var second = await Register(w, _app.AddParticipant("Bob"));
        var third = await Register(w, _app.AddParticipant("Carol"));

        await _app.NewService().CancelAsync(first.Id);
        var again = await _app.NewService().CancelAsync(first.Id);

        Assert.Equal(RegistrationStatus.Cancelled, again.Status);
        Assert.Equal(RegistrationStatus.Confirmed, _app.StatusOf(second.Id));
        Assert.Equal(RegistrationStatus.Waitlisted, _app.StatusOf(third.Id));
        Assert.Single(_app.Notifier.Sent, n => n.Type == NotificationType.RegistrationCancelled);
    }

    [Fact]
    public async Task Cancelling_unknown_registration_is_not_found()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() => _app.NewService().CancelAsync(999));
        Assert.Equal(DomainErrorKind.NotFound, ex.Kind);
    }

    [Fact]
    public async Task Only_one_of_many_concurrent_registrants_gets_the_final_seat()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 1);
        var participants = Enumerable.Range(0, 25).Select(i => _app.AddParticipant($"P{i}")).ToList();

        var results = await Task.WhenAll(participants.Select(p => Task.Run(async () =>
            (await _app.NewService().RegisterAsync(w, p, null)).Registration.Status)));

        Assert.Single(results, s => s == RegistrationStatus.Confirmed);
        Assert.Equal(24, results.Count(s => s == RegistrationStatus.Waitlisted));
        using var db = _app.NewDb();
        Assert.Equal(1, db.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Concurrent_registrations_never_exceed_capacity_with_partially_filled_workshop()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 5);
        await Register(w, _app.AddParticipant("Early"));
        var participants = Enumerable.Range(0, 20).Select(i => _app.AddParticipant($"P{i}")).ToList();

        await Task.WhenAll(participants.Select(p => Task.Run(() => _app.NewService().RegisterAsync(w, p, null))));

        using var db = _app.NewDb();
        Assert.Equal(5, db.Registrations.Count(r => r.Status == RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Concurrent_requests_with_same_idempotency_key_create_one_registration()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 5);
        var alice = _app.AddParticipant("Alice");

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() => _app.NewService().RegisterAsync(w, alice, "same-key"))));

        Assert.Single(results.Select(r => r.Registration.Id).Distinct());
        Assert.Single(results, r => r.Created);
    }

    [Fact]
    public async Task Notification_failure_does_not_roll_back_registration_or_cancellation()
    {
        var w = _app.AddWorkshop("A", 9, 10, capacity: 1);
        var alice = _app.AddParticipant("Alice");
        var bob = _app.AddParticipant("Bob");
        var failing = new ThrowingNotifier();

        var first = (await _app.NewService(failing).RegisterAsync(w, alice, null)).Registration;
        var second = (await _app.NewService(failing).RegisterAsync(w, bob, null)).Registration;
        await _app.NewService(failing).CancelAsync(first.Id);

        Assert.Equal(RegistrationStatus.Cancelled, _app.StatusOf(first.Id));
        Assert.Equal(RegistrationStatus.Confirmed, _app.StatusOf(second.Id));
    }
}
