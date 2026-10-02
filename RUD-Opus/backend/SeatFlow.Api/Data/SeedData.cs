using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Data;

public static class SeedData
{
    /// <summary>Seeds a one-day conference (all times UTC) when the database is empty.</summary>
    public static void SeedIfEmpty(SeatFlowDbContext db)
    {
        if (db.Workshops.Any() || db.Participants.Any()) return;

        var day = new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc);
        Workshop W(string title, double startHour, double endHour, int capacity) =>
            new() { Title = title, StartTime = day.AddHours(startHour), EndTime = day.AddHours(endHour), Capacity = capacity };

        var eventSourcing = W("Intro to Event Sourcing", 9, 10.5, 3);
        var ddd = W("Practical Domain-Driven Design", 10, 11.5, 2);          // overlaps Event Sourcing 10:00-10:30
        var a11y = W("Accessible Front-ends in Practice", 11.75, 13, 4);
        var concurrency = W("Testing Concurrency in .NET", 13.5, 15, 2);
        var kubernetes = W("Kubernetes Without Tears", 14.5, 16, 20);       // overlaps Concurrency 14:30-15:00
        db.Workshops.AddRange(eventSourcing, ddd, a11y, concurrency, kubernetes);

        Participant P(string name, string email) => new() { Name = name, Email = email };
        var ada = P("Ada Lovelace", "ada@example.com");
        var grace = P("Grace Hopper", "grace@example.com");
        var alan = P("Alan Turing", "alan@example.com");
        var margaret = P("Margaret Hamilton", "margaret@example.com");
        var linus = P("Linus Torvalds", "linus@example.com");
        var barbara = P("Barbara Liskov", "barbara@example.com");
        db.Participants.AddRange(ada, grace, alan, margaret, linus, barbara);

        // A full workshop with a waitlist whose first entry (Alan) has a schedule conflict, so cancelling a
        // confirmed DDD seat demonstrates skipping him and promoting Margaret.
        var t = DateTime.UtcNow.AddDays(-7);
        Registration R(Workshop w, Participant p, RegistrationStatus s) =>
            new() { Workshop = w, Participant = p, Status = s, CreatedAt = t = t.AddMinutes(5) };
        db.Registrations.AddRange(
            R(eventSourcing, alan, RegistrationStatus.Confirmed),
            R(ddd, ada, RegistrationStatus.Confirmed),
            R(ddd, grace, RegistrationStatus.Confirmed),
            R(ddd, alan, RegistrationStatus.Waitlisted),
            R(ddd, margaret, RegistrationStatus.Waitlisted),
            R(concurrency, linus, RegistrationStatus.Confirmed),
            R(a11y, barbara, RegistrationStatus.Confirmed));

        db.SaveChanges();
    }
}
