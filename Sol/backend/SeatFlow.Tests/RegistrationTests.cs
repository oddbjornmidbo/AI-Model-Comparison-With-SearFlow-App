using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeatFlow.Api;
using Xunit;

namespace SeatFlow.Tests;

public sealed class RegistrationTests : IAsyncLifetime
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"seatflow-test-{Guid.NewGuid():N}.db");
    private readonly RecordingNotifications notifications = new();
    private SeatFlowDb Db() => new(new DbContextOptionsBuilder<SeatFlowDb>().UseSqlite($"Data Source={path};Default Timeout=10").Options);
    private RegistrationService Service(SeatFlowDb db) => new(db, notifications, NullLogger<RegistrationService>.Instance);
    public async Task InitializeAsync()
    {
        await using var db = Db(); await db.Database.EnsureCreatedAsync();
        var start = new DateTime(2030, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        db.Workshops.AddRange(new Workshop { Id=1, Title="Main", StartTime=start, EndTime=start.AddHours(2), Capacity=1 }, new Workshop { Id=2, Title="Overlap", StartTime=start.AddHours(1), EndTime=start.AddHours(3), Capacity=2 }, new Workshop { Id=3, Title="Later", StartTime=start.AddHours(3), EndTime=start.AddHours(4), Capacity=2 });
        for (var i=1; i<=5; i++) db.Participants.Add(new Participant { Id=i, Name=$"Person {i}", Email=$"p{i}@example.com" });
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() { if (File.Exists(path)) File.Delete(path); return Task.CompletedTask; }

    [Fact] public async Task AvailableSeatIsConfirmedAndFullWorkshopWaitlists()
    {
        await using var db = Db(); var service = Service(db);
        var first = await service.Register(1, 1, null); var second = await service.Register(1, 2, null);
        Assert.Equal(RegistrationStatus.Confirmed, first.Status); Assert.Equal(RegistrationStatus.Waitlisted, second.Status);
        Assert.Contains(notifications.Sent, n => n.Kind == "Confirmed" && n.RegistrationId == first.Id);
    }
    [Fact] public async Task CancellationPromotesOldestEligibleWaitlistedParticipant()
    {
        await using var db = Db(); var service = Service(db);
        var first = await service.Register(1, 1, null); var oldest = await service.Register(1, 2, null); var later = await service.Register(1, 3, null);
        await service.Cancel(first.Id);
        Assert.Equal(RegistrationStatus.Confirmed, oldest.Status); Assert.Equal(RegistrationStatus.Waitlisted, later.Status);
        Assert.Contains(notifications.Sent, n => n.Kind == "Promoted" && n.RegistrationId == oldest.Id);
    }
    [Fact] public async Task PromotionSkipsConflictAndLeavesSkippedRegistrationWaitlisted()
    {
        await using var db = Db(); var service = Service(db);
        var holder = await service.Register(1, 1, null);
        await service.Register(2, 2, null);
        var skipped = await service.Register(1, 2, null); var eligible = await service.Register(1, 3, null);
        await service.Cancel(holder.Id);
        Assert.Equal(RegistrationStatus.Waitlisted, skipped.Status); Assert.Equal(RegistrationStatus.Confirmed, eligible.Status);
    }
    [Fact] public async Task OverlappingConfirmedWorkshopsAreRejectedButLaterWorkshopIsAllowed()
    {
        await using var db = Db(); var service = Service(db);
        await service.Register(1, 1, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Register(2, 1, null));
        Assert.Equal(RegistrationStatus.Confirmed, (await service.Register(3, 1, null)).Status);
    }
    [Fact] public async Task DuplicateActiveRegistrationIsRejectedAndCancelledOneMayBeReplaced()
    {
        await using var db = Db(); var service = Service(db);
        var first = await service.Register(1, 1, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Register(1, 1, null));
        await service.Cancel(first.Id);
        Assert.NotEqual(first.Id, (await service.Register(1, 1, null)).Id);
    }
    [Fact] public async Task IdempotencyKeyReturnsSameRegistrationAndNeverDuplicates()
    {
        await using var db = Db(); var service = Service(db);
        var first = await service.Register(1, 1, "request-1"); var again = await service.Register(1, 1, "request-1");
        Assert.Equal(first.Id, again.Id); Assert.Equal(1, await db.Registrations.CountAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Register(1, 2, "request-1"));
    }
    [Fact] public async Task CancellingTwiceKeepsSameStateAndEmitsOneCancellation()
    {
        await using var db = Db(); var service = Service(db);
        var first = await service.Register(1, 1, null);
        await service.Cancel(first.Id); await service.Cancel(first.Id);
        Assert.Equal(RegistrationStatus.Cancelled, first.Status);
        Assert.Single(notifications.Sent, n => n.Kind == "Cancelled");
    }
    [Fact] public async Task ConcurrentAttemptsForLastSeatConfirmOnlyOne()
    {
        async Task<RegistrationStatus> Register(int participant)
        {
            await using var db = Db(); return (await Service(db).Register(1, participant, null)).Status;
        }
        var statuses = await Task.WhenAll(Register(1), Register(2));
        Assert.Single(statuses, s => s == RegistrationStatus.Confirmed);
        Assert.Single(statuses, s => s == RegistrationStatus.Waitlisted);
        await using var check = Db(); Assert.Equal(1, await check.Registrations.CountAsync(r => r.Status == RegistrationStatus.Confirmed));
    }
    [Fact] public async Task NotificationFailureDoesNotUndoRegistrationOrCancellation()
    {
        await using var db = Db(); var service = new RegistrationService(db, new FailingNotifications(), NullLogger<RegistrationService>.Instance);
        var registration = await service.Register(1, 1, null); await service.Cancel(registration.Id);
        Assert.Equal(RegistrationStatus.Cancelled, (await db.Registrations.SingleAsync()).Status);
    }
    private sealed class RecordingNotifications : INotificationService
    {
        public List<Notification> Sent { get; } = [];
        public Task NotifyAsync(string kind, Registration registration) { lock (Sent) Sent.Add(new(kind, registration.Id, registration.ParticipantId, registration.WorkshopId, DateTime.UtcNow)); return Task.CompletedTask; }
    }
    private sealed class FailingNotifications : INotificationService { public Task NotifyAsync(string kind, Registration registration) => throw new Exception("Delivery failed"); }
}
