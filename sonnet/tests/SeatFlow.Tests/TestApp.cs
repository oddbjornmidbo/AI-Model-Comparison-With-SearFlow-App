using Microsoft.EntityFrameworkCore;
using SeatFlow.Data;
using SeatFlow.Domain;
using SeatFlow.Services;

namespace SeatFlow.Tests;

/// <summary>Isolated SQLite database per test class instance, with helpers to set up data and create fresh services.</summary>
public sealed class TestApp : IDisposable
{
    public static readonly DateTime Day = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"seatflow-test-{Guid.NewGuid():N}.db");
    private readonly DbContextOptions<SeatFlowDbContext> _options;

    public RecordingNotifier Notifier { get; } = new();

    public TestApp()
    {
        _options = new DbContextOptionsBuilder<SeatFlowDbContext>().UseSqlite($"Data Source={_path}").Options;
        using var db = NewDb();
        db.Database.EnsureCreated();
    }

    public SeatFlowDbContext NewDb() => new(_options);

    /// <summary>A new service with its own DbContext, like one per HTTP request.</summary>
    public RegistrationService NewService(INotificationService? notifier = null) =>
        new(NewDb(), notifier ?? Notifier, Microsoft.Extensions.Logging.Abstractions.NullLogger<RegistrationService>.Instance);

    public int AddWorkshop(string title, double startHour, double endHour, int capacity)
    {
        using var db = NewDb();
        var w = new Workshop { Title = title, StartTime = Day.AddHours(startHour), EndTime = Day.AddHours(endHour), Capacity = capacity };
        db.Workshops.Add(w);
        db.SaveChanges();
        return w.Id;
    }

    public int AddParticipant(string name)
    {
        using var db = NewDb();
        var p = new Participant { Name = name, Email = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com" };
        db.Participants.Add(p);
        db.SaveChanges();
        return p.Id;
    }

    public RegistrationStatus StatusOf(int registrationId)
    {
        using var db = NewDb();
        return db.Registrations.Single(r => r.Id == registrationId).Status;
    }

    public string DbPath => _path;

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var f in new[] { _path, _path + "-shm", _path + "-wal" })
            try { File.Delete(f); } catch { /* best effort */ }
    }
}

public class RecordingNotifier : INotificationService
{
    private readonly List<Notification> _sent = [];
    public IReadOnlyList<Notification> Sent { get { lock (_sent) return _sent.ToArray(); } }

    public Task SendAsync(Notification n, CancellationToken ct = default)
    {
        lock (_sent) _sent.Add(n);
        return Task.CompletedTask;
    }
}

public class ThrowingNotifier : INotificationService
{
    public Task SendAsync(Notification n, CancellationToken ct = default) => throw new InvalidOperationException("notification outage");
}
