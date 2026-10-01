using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IDbContextFactory<SeatFlowDbContext> factory, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        if (await db.Workshops.AnyAsync(cancellationToken))
            return;

        var now = DateTimeOffset.UtcNow;
        var day = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero).AddDays(14);
        db.Workshops.AddRange(
            new Workshop { Title = "Designing Better APIs", StartTime = day.AddHours(9), EndTime = day.AddHours(11), Capacity = 3 },
            new Workshop { Title = "Practical TypeScript", StartTime = day.AddHours(10), EndTime = day.AddHours(12), Capacity = 2 },
            new Workshop { Title = "SQLite in Practice", StartTime = day.AddHours(12), EndTime = day.AddHours(14), Capacity = 4 },
            new Workshop { Title = "Product Discovery Lab", StartTime = day.AddHours(14), EndTime = day.AddHours(16), Capacity = 3 });
        db.Participants.AddRange(
            new Participant { Name = "Avery Chen", Email = "avery@example.com" },
            new Participant { Name = "Jordan Patel", Email = "jordan@example.com" },
            new Participant { Name = "Morgan Lee", Email = "morgan@example.com" },
            new Participant { Name = "Riley Garcia", Email = "riley@example.com" },
            new Participant { Name = "Casey Nguyen", Email = "casey@example.com" },
            new Participant { Name = "Taylor Smith", Email = "taylor@example.com" });
        await db.SaveChangesAsync(cancellationToken);
    }
}
