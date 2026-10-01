using SeatFlow.Domain;

namespace SeatFlow.Data;

public static class DbSeeder
{
    public static void Seed(SeatFlowDbContext db)
    {
        db.Database.EnsureCreated();
        if (db.Workshops.Any()) return;

        var day = DateTime.UtcNow.Date.AddDays(1);
        var ddd = new Workshop { Title = "Domain-Driven Design", StartTime = day.AddHours(9), EndTime = day.AddHours(11), Capacity = 2 };
        var k8s = new Workshop { Title = "Hands-on Kubernetes", StartTime = day.AddHours(10.5), EndTime = day.AddHours(12.5), Capacity = 2 };  // overlaps DDD
        var sec = new Workshop { Title = "API Security Basics", StartTime = day.AddHours(13), EndTime = day.AddHours(15), Capacity = 3 };
        var rct = new Workshop { Title = "Testing React Apps", StartTime = day.AddHours(15), EndTime = day.AddHours(17), Capacity = 2 };   // back-to-back with Security
        var perf = new Workshop { Title = "Performance Tuning", StartTime = day.AddHours(9.5), EndTime = day.AddHours(10.75), Capacity = 1 }; // overlaps DDD and K8s
        db.Workshops.AddRange(ddd, k8s, sec, rct, perf);

        var alice = new Participant { Name = "Alice Hansen", Email = "alice@example.com" };
        var bob = new Participant { Name = "Bob Larsen", Email = "bob@example.com" };
        var carol = new Participant { Name = "Carol Olsen", Email = "carol@example.com" };
        var dave = new Participant { Name = "Dave Berg", Email = "dave@example.com" };
        var erin = new Participant { Name = "Erin Moe", Email = "erin@example.com" };
        var frank = new Participant { Name = "Frank Dahl", Email = "frank@example.com" };
        db.Participants.AddRange(alice, bob, carol, dave, erin, frank);

        // Demo scenario: DDD is full; Carol is first on the waitlist but already confirmed in the
        // overlapping Kubernetes workshop, so cancelling Alice promotes Dave instead of Carol.
        var t = DateTime.UtcNow.AddMinutes(-10);
        void Reg(Workshop w, Participant p, RegistrationStatus s, int secs) =>
            db.Registrations.Add(new Registration { Workshop = w, Participant = p, Status = s, CreatedAt = t.AddSeconds(secs) });

        Reg(ddd, alice, RegistrationStatus.Confirmed, 0);
        Reg(ddd, bob, RegistrationStatus.Confirmed, 1);
        Reg(k8s, carol, RegistrationStatus.Confirmed, 2);
        Reg(ddd, carol, RegistrationStatus.Waitlisted, 3);
        Reg(ddd, dave, RegistrationStatus.Waitlisted, 4);
        Reg(sec, erin, RegistrationStatus.Confirmed, 5);

        db.SaveChanges();
    }
}
