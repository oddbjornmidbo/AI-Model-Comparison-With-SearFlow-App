using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Data;

public static class SeedData
{
    public static void Seed(SeatFlowDbContext db)
    {
        if (db.Workshops.Any() || db.Participants.Any())
            return;

        // Conference day: 2026-11-12 (UTC).
        static DateTime At(int hour, int minute = 0) => new(2026, 11, 12, hour, minute, 0, DateTimeKind.Utc);

        var workshops = new[]
        {
            // "Intro to EF Core" and "Async C# Deep Dive" partially overlap (10:30–11:00).
            new Workshop { Title = "Intro to EF Core", StartTime = At(9), EndTime = At(11), Capacity = 3 },
            new Workshop { Title = "Async C# Deep Dive", StartTime = At(10, 30), EndTime = At(12, 30), Capacity = 2 },
            new Workshop { Title = "React Hooks in Practice", StartTime = At(13), EndTime = At(15), Capacity = 4 },
            new Workshop { Title = "Testing Strategies", StartTime = At(14), EndTime = At(16), Capacity = 2 },
            new Workshop { Title = "Closing Panel", StartTime = At(16), EndTime = At(17), Capacity = 50 },
        };

        var participants = new[]
        {
            new Participant { Name = "Ada Lovelace", Email = "ada@example.com" },
            new Participant { Name = "Alan Turing", Email = "alan@example.com" },
            new Participant { Name = "Grace Hopper", Email = "grace@example.com" },
            new Participant { Name = "Linus Torvalds", Email = "linus@example.com" },
            new Participant { Name = "Margaret Hamilton", Email = "margaret@example.com" },
            new Participant { Name = "Ken Thompson", Email = "ken@example.com" },
        };

        db.Workshops.AddRange(workshops);
        db.Participants.AddRange(participants);
        db.SaveChanges();
    }
}
