using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Services;
using Xunit;

namespace SeatFlow.Tests;

public sealed class RegistrationServiceTests
{
    [Fact]
    public async Task Registers_into_available_workshop_as_confirmed()
    {
        await using var app = await TestApp.CreateAsync(capacity: 2);
        var result = await app.Service.RegisterAsync(1, 1, "available-1");

        Assert.Equal(RegistrationFailure.None, result.Failure);
        Assert.Equal(RegistrationStatus.Confirmed, result.Registration!.Status);
        Assert.Single(app.Notifications.Items, x => x.Type == "confirmed");
    }

    [Fact]
    public async Task Registers_into_full_workshop_as_waitlisted()
    {
        await using var app = await TestApp.CreateAsync(capacity: 1);
        Assert.Equal(RegistrationStatus.Confirmed, (await app.Service.RegisterAsync(1, 1, "full-1")).Registration!.Status);
        var second = await app.Service.RegisterAsync(1, 2, "full-2");

        Assert.Equal(RegistrationStatus.Waitlisted, second.Registration!.Status);
    }

    [Fact]
    public async Task Cancellation_promotes_waitlist_in_fifo_order()
    {
        await using var app = await TestApp.CreateAsync(capacity: 1);
        var first = (await app.Service.RegisterAsync(1, 1, "fifo-1")).Registration!;
        var second = (await app.Service.RegisterAsync(1, 2, "fifo-2")).Registration!;
        var third = (await app.Service.RegisterAsync(1, 3, "fifo-3")).Registration!;

        var cancelled = await app.Service.CancelAsync(first.Id);

        Assert.Equal(second.Id, cancelled.Promoted!.Id);
        Assert.Equal(RegistrationStatus.Confirmed, await app.GetStatusAsync(second.Id));
        Assert.Equal(RegistrationStatus.Waitlisted, await app.GetStatusAsync(third.Id));
        Assert.Single(app.Notifications.Items, x => x.Type == "promoted" && x.RegistrationId == second.Id);
    }

    [Fact]
    public async Task Promotion_skips_oldest_waitlisted_participant_with_schedule_conflict()
    {
        await using var app = await TestApp.CreateAsync(capacity: 1);
        var occupying = (await app.Service.RegisterAsync(1, 1, "skip-occupying")).Registration!;
        var ineligible = (await app.Service.RegisterAsync(1, 2, "skip-ineligible")).Registration!;
        var eligible = (await app.Service.RegisterAsync(1, 3, "skip-eligible")).Registration!;
        var overlapping = await app.Service.RegisterAsync(2, 2, "skip-other-workshop");
        Assert.Equal(RegistrationStatus.Confirmed, overlapping.Registration!.Status);

        var cancelled = await app.Service.CancelAsync(occupying.Id);

        Assert.Equal(eligible.Id, cancelled.Promoted!.Id);
        Assert.Equal(RegistrationStatus.Waitlisted, await app.GetStatusAsync(ineligible.Id));
        Assert.Equal(RegistrationStatus.Confirmed, await app.GetStatusAsync(eligible.Id));
    }

    [Fact]
    public async Task Rejects_confirmed_registration_that_overlaps_another_workshop()
    {
        await using var app = await TestApp.CreateAsync(capacity: 2);
        Assert.Equal(RegistrationStatus.Confirmed, (await app.Service.RegisterAsync(1, 1, "overlap-1")).Registration!.Status);

        var result = await app.Service.RegisterAsync(2, 1, "overlap-2");

        Assert.Equal(RegistrationFailure.ScheduleConflict, result.Failure);
        Assert.Null(result.Registration);
    }

    [Fact]
    public async Task Prevents_duplicate_active_registration_even_with_a_different_key()
    {
        await using var app = await TestApp.CreateAsync(capacity: 2);
        var first = await app.Service.RegisterAsync(1, 1, "duplicate-1");
        var second = await app.Service.RegisterAsync(1, 1, "duplicate-2");

        Assert.Equal(RegistrationFailure.None, first.Failure);
        Assert.Equal(RegistrationFailure.AlreadyRegistered, second.Failure);
        Assert.Equal(1, await app.CountRegistrationsAsync());
    }

    [Fact]
    public async Task Allows_a_new_registration_after_the_previous_one_was_cancelled()
    {
        await using var app = await TestApp.CreateAsync(capacity: 2);
        var first = (await app.Service.RegisterAsync(1, 1, "reregister-first")).Registration!;
        await app.Service.CancelAsync(first.Id);

        var replacement = await app.Service.RegisterAsync(1, 1, "reregister-second");

        Assert.Equal(RegistrationStatus.Confirmed, replacement.Registration!.Status);
        Assert.NotEqual(first.Id, replacement.Registration.Id);
        Assert.Equal(2, await app.CountRegistrationsAsync());
    }

    [Fact]
    public async Task Repeated_idempotency_key_returns_the_same_registration()
    {
        await using var app = await TestApp.CreateAsync(capacity: 2);
        var first = await app.Service.RegisterAsync(1, 1, "idempotent-key");
        var repeated = await app.Service.RegisterAsync(1, 1, "idempotent-key");

        Assert.Equal(first.Registration!.Id, repeated.Registration!.Id);
        Assert.True(repeated.WasReplayed);
        Assert.Equal(1, await app.CountRegistrationsAsync());
    }

    [Fact]
    public async Task Rejects_reuse_of_idempotency_key_for_different_request()
    {
        await using var app = await TestApp.CreateAsync(capacity: 2);
        await app.Service.RegisterAsync(1, 1, "same-key");

        var result = await app.Service.RegisterAsync(1, 2, "same-key");

        Assert.Equal(RegistrationFailure.IdempotencyKeyConflict, result.Failure);
    }

    [Fact]
    public async Task Cancelling_twice_is_idempotent()
    {
        await using var app = await TestApp.CreateAsync(capacity: 2);
        var registration = (await app.Service.RegisterAsync(1, 1, "cancel-twice")).Registration!;

        var first = await app.Service.CancelAsync(registration.Id);
        var second = await app.Service.CancelAsync(registration.Id);

        Assert.False(first.WasAlreadyCancelled);
        Assert.True(second.WasAlreadyCancelled);
        Assert.Equal(RegistrationStatus.Cancelled, second.Registration!.Status);
        Assert.Single(app.Notifications.Items, x => x.Type == "cancelled");
    }

    [Fact]
    public async Task Concurrent_attempts_for_final_seat_never_overbook_capacity()
    {
        await using var app = await TestApp.CreateAsync(capacity: 1);
        var attempts = Enumerable.Range(1, 6)
            .Select(participantId => app.Service.RegisterAsync(1, participantId, $"concurrent-{participantId}"));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(x => x.Registration!.Status == RegistrationStatus.Confirmed));
        Assert.Equal(5, results.Count(x => x.Registration!.Status == RegistrationStatus.Waitlisted));
    }

    [Fact]
    public async Task Notification_failure_does_not_undo_committed_registration()
    {
        await using var app = await TestApp.CreateAsync(capacity: 1, new ThrowingNotificationService());

        var result = await app.Service.RegisterAsync(1, 1, "notify-failure");

        Assert.Equal(RegistrationStatus.Confirmed, result.Registration!.Status);
        Assert.Equal(1, await app.CountRegistrationsAsync());
    }

    private sealed class TestApp : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly PooledDbContextFactory<SeatFlowDbContext> _factory;
        public RegistrationService Service { get; }
        public RecordingNotificationService Notifications { get; }

        private TestApp(SqliteConnection connection, PooledDbContextFactory<SeatFlowDbContext> factory, RegistrationService service, RecordingNotificationService notifications)
        {
            _connection = connection;
            _factory = factory;
            Service = service;
            Notifications = notifications;
        }

        public static async Task<TestApp> CreateAsync(int capacity, INotificationService? notifications = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SeatFlowDbContext>().UseSqlite(connection).Options;
            var factory = new PooledDbContextFactory<SeatFlowDbContext>(options);
            await using (var db = await factory.CreateDbContextAsync())
            {
                await db.Database.EnsureCreatedAsync();
                var day = DateTimeOffset.UtcNow.Date.AddDays(2);
                db.Workshops.AddRange(
                    new Workshop { Id = 1, Title = "Workshop One", StartTime = day.AddHours(10), EndTime = day.AddHours(11), Capacity = capacity },
                    new Workshop { Id = 2, Title = "Workshop Two", StartTime = day.AddHours(10).AddMinutes(30), EndTime = day.AddHours(11).AddMinutes(30), Capacity = 1 });
                for (var i = 1; i <= 6; i++)
                    db.Participants.Add(new Participant { Id = i, Name = $"Participant {i}", Email = $"p{i}@example.test" });
                await db.SaveChangesAsync();
            }
            var recording = notifications as RecordingNotificationService ?? new RecordingNotificationService();
            var actualNotifications = notifications ?? recording;
            var service = new RegistrationService(factory, actualNotifications, NullLogger<RegistrationService>.Instance);
            return new TestApp(connection, factory, service, recording);
        }

        public async Task<RegistrationStatus> GetStatusAsync(int registrationId)
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Registrations.Where(x => x.Id == registrationId).Select(x => x.Status).SingleAsync();
        }

        public async Task<int> CountRegistrationsAsync()
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Registrations.CountAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<RegistrationNotification> Items { get; } = [];
        public Task SendAsync(RegistrationNotification notification, CancellationToken cancellationToken = default)
        {
            Items.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingNotificationService : INotificationService
    {
        public Task SendAsync(RegistrationNotification notification, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Expected test notification failure.");
    }
}
