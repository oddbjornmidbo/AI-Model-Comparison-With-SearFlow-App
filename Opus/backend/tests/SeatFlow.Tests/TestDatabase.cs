using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Notifications;
using SeatFlow.Api.Services;

namespace SeatFlow.Tests;

/// <summary>A real, file-backed SQLite database per test so locking behaves like production.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"seatflow-test-{Guid.NewGuid():N}.db");

    public RecordingNotificationService Notifications { get; } = new();
    public string ConnectionString => $"Data Source={_path}";

    public TestDatabase()
    {
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public SeatFlowDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<SeatFlowDbContext>().UseSqlite(ConnectionString).Options);

    public RegistrationService CreateService(SeatFlowDbContext db, INotificationService? notifications = null) =>
        new(db, notifications ?? Notifications, TimeProvider.System, NullLogger<RegistrationService>.Instance);

    /// <summary>Runs one operation in its own context, like one HTTP request would.</summary>
    public async Task<T> Run<T>(Func<RegistrationService, Task<T>> action)
    {
        await using var db = CreateContext();
        return await action(CreateService(db));
    }

    public Task<RegisterResult> Register(Workshop w, Participant p, string? key = null) =>
        Run(s => s.RegisterAsync(w.Id, p.Id, key));

    public Task<CancelResult> Cancel(int registrationId) => Run(s => s.CancelAsync(registrationId));

    public async Task<RegistrationStatus> StatusOf(int registrationId)
    {
        await using var db = CreateContext();
        return (await db.Registrations.SingleAsync(r => r.Id == registrationId)).Status;
    }

    public async Task<Workshop> AddWorkshop(string title, string start, string end, int capacity)
    {
        await using var db = CreateContext();
        var w = new Workshop
        {
            Title = title,
            StartTime = DateTime.SpecifyKind(DateTime.Parse($"2026-11-12T{start}"), DateTimeKind.Utc),
            EndTime = DateTime.SpecifyKind(DateTime.Parse($"2026-11-12T{end}"), DateTimeKind.Utc),
            Capacity = capacity,
        };
        db.Workshops.Add(w);
        await db.SaveChangesAsync();
        return w;
    }

    public async Task<Participant> AddParticipant(string name)
    {
        await using var db = CreateContext();
        var p = new Participant { Name = name, Email = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com" };
        db.Participants.Add(p);
        await db.SaveChangesAsync();
        return p;
    }

    public async Task<List<Participant>> AddParticipants(int count)
    {
        var result = new List<Participant>();
        for (var i = 1; i <= count; i++)
            result.Add(await AddParticipant($"P{i}"));
        return result;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { }
    }
}

public sealed class RecordingNotificationService : INotificationService
{
    private readonly List<Notification> _sent = [];

    public IReadOnlyList<Notification> Sent { get { lock (_sent) return _sent.ToList(); } }

    public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        lock (_sent) _sent.Add(notification);
        return Task.CompletedTask;
    }
}

public sealed class FailingNotificationService : INotificationService
{
    public Task SendAsync(Notification notification, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Mail server is down");
}
