using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Services;

namespace SeatFlow.Tests;

public sealed class ConcurrencyTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    /// <summary>Starts all actions at (roughly) the same instant, each with its own DbContext/connection.</summary>
    private static async Task<T[]> RunSimultaneously<T>(int count, Func<int, Task<T>> action)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, count)
            .Select(i => Task.Run(async () => { await start.Task; return await action(i); }))
            .ToArray();
        start.SetResult();
        return await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Only_one_of_many_simultaneous_attempts_gets_the_final_seat()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 3);
        var early = await _db.AddParticipants(2);
        foreach (var p in early) await _db.Register(workshop, p);
        var racers = new List<Participant>();
        for (var i = 0; i < 12; i++) racers.Add(await _db.AddParticipant($"Racer{i}"));

        var results = await RunSimultaneously(racers.Count, i => _db.Register(workshop, racers[i]));

        Assert.Single(results, r => r.Registration.Status == RegistrationStatus.Confirmed);
        Assert.Equal(racers.Count - 1, results.Count(r => r.Registration.Status == RegistrationStatus.Waitlisted));
        await using var db = _db.CreateContext();
        Assert.Equal(3, await db.Registrations.CountAsync(r => r.WorkshopId == workshop.Id && r.Status == RegistrationStatus.Confirmed));
    }

    [Fact]
    public async Task Simultaneous_requests_with_the_same_idempotency_key_create_one_registration()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 5);
        var ada = await _db.AddParticipant("Ada");

        var results = await RunSimultaneously(8, _ => _db.Register(workshop, ada, key: "retry-storm"));

        Assert.Single(results.Select(r => r.Registration.Id).Distinct());
        Assert.Single(results, r => !r.Replayed);
        await using var db = _db.CreateContext();
        Assert.Equal(1, await db.Registrations.CountAsync());
    }

    [Fact]
    public async Task Simultaneous_duplicate_registrations_without_key_create_only_one()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 5);
        var ada = await _db.AddParticipant("Ada");

        var outcomes = await RunSimultaneously(8, async _ =>
        {
            try { await _db.Register(workshop, ada); return "ok"; }
            catch (DomainException ex) when (ex.Error == DomainError.AlreadyRegistered) { return "dup"; }
        });

        Assert.Single(outcomes, o => o == "ok");
        Assert.Equal(7, outcomes.Count(o => o == "dup"));
    }

    [Fact]
    public async Task Simultaneous_cancellations_of_the_same_registration_promote_exactly_once()
    {
        var workshop = await _db.AddWorkshop("EF", "09:00", "10:00", capacity: 1);
        var p = await _db.AddParticipants(3);
        var holder = await _db.Register(workshop, p[0]);
        await _db.Register(workshop, p[1]);
        await _db.Register(workshop, p[2]);

        var results = await RunSimultaneously(6, _ => _db.Cancel(holder.Registration.Id));

        Assert.Single(results, r => !r.AlreadyCancelled);
        Assert.Equal(1, results.Sum(r => r.Promoted.Count));
        await using var db = _db.CreateContext();
        Assert.Equal(1, await db.Registrations.CountAsync(r => r.Status == RegistrationStatus.Confirmed));
    }
}
