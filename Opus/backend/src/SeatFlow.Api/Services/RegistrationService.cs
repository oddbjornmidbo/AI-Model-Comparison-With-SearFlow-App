using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;
using SeatFlow.Api.Notifications;

namespace SeatFlow.Api.Services;

public sealed record RegisterResult(Registration Registration, bool Replayed);

public sealed record CancelResult(Registration Registration, bool AlreadyCancelled, IReadOnlyList<Registration> Promoted);

/// <summary>
/// Owns all registration state transitions (register, cancel, waitlist promotion).
/// Every write runs inside a SQLite write transaction taken up front (BEGIN IMMEDIATE), so the
/// read-check-write sequence for capacity, duplicates and idempotency keys is serialized.
/// Notifications are dispatched only after commit and never affect the outcome.
/// </summary>
public sealed class RegistrationService(
    SeatFlowDbContext db,
    INotificationService notifications,
    TimeProvider clock,
    ILogger<RegistrationService> logger)
{
    public async Task<RegisterResult> RegisterAsync(
        int workshopId, int participantId, string? idempotencyKey, CancellationToken ct = default)
    {
        Registration registration;

        await using (var tx = await BeginWriteTransactionAsync(ct))
        {
            if (idempotencyKey is not null)
            {
                var existing = await db.Registrations
                    .Include(r => r.Workshop)
                    .Include(r => r.Participant)
                    .SingleOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);

                if (existing is not null)
                {
                    if (existing.WorkshopId != workshopId || existing.ParticipantId != participantId)
                        throw new DomainException(DomainError.IdempotencyKeyReused,
                            "This Idempotency-Key was already used for a different registration request.");
                    return new RegisterResult(existing, Replayed: true);
                }
            }

            var workshop = await db.Workshops.FindAsync([workshopId], ct)
                ?? throw new DomainException(DomainError.NotFound, $"Workshop {workshopId} was not found.");
            var participant = await db.Participants.FindAsync([participantId], ct)
                ?? throw new DomainException(DomainError.NotFound, $"Participant {participantId} was not found.");

            var alreadyRegistered = await db.Registrations.AnyAsync(r =>
                r.WorkshopId == workshopId &&
                r.ParticipantId == participantId &&
                r.Status != RegistrationStatus.Cancelled, ct);
            if (alreadyRegistered)
                throw new DomainException(DomainError.AlreadyRegistered,
                    $"{participant.Name} already has an active registration for '{workshop.Title}'.");

            var hasSeat = await CountConfirmedAsync(workshopId, ct) < workshop.Capacity;
            if (hasSeat && await HasScheduleConflictAsync(participantId, workshop, ct))
                throw new DomainException(DomainError.ScheduleConflict,
                    $"{participant.Name} already has a confirmed workshop overlapping '{workshop.Title}'.");

            registration = new Registration
            {
                Workshop = workshop,
                Participant = participant,
                Status = hasSeat ? RegistrationStatus.Confirmed : RegistrationStatus.Waitlisted,
                CreatedAt = clock.GetUtcNow().UtcDateTime,
                IdempotencyKey = idempotencyKey,
            };
            db.Registrations.Add(registration);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        if (registration.Status == RegistrationStatus.Confirmed)
            await NotifyAsync(NotificationType.RegistrationConfirmed, registration);

        return new RegisterResult(registration, Replayed: false);
    }

    public async Task<CancelResult> CancelAsync(int registrationId, CancellationToken ct = default)
    {
        Registration registration;
        var promoted = new List<Registration>();

        await using (var tx = await BeginWriteTransactionAsync(ct))
        {
            registration = await db.Registrations
                .Include(r => r.Workshop)
                .Include(r => r.Participant)
                .SingleOrDefaultAsync(r => r.Id == registrationId, ct)
                ?? throw new DomainException(DomainError.NotFound, $"Registration {registrationId} was not found.");

            if (registration.Status == RegistrationStatus.Cancelled)
                return new CancelResult(registration, AlreadyCancelled: true, Promoted: []);

            var wasConfirmed = registration.Status == RegistrationStatus.Confirmed;
            registration.Status = RegistrationStatus.Cancelled;
            registration.CancelledAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);

            if (wasConfirmed)
            {
                // The freed seat goes to the first eligible person on this workshop's waitlist.
                promoted.AddRange(await PromoteFromWaitlistAsync(registration.Workshop, ct));

                // The cancelling participant's calendar just opened up, which may make them eligible
                // for a seat elsewhere that was left empty because everyone waiting there had a conflict.
                var waitlistedElsewhere = await db.Registrations
                    .Where(r => r.ParticipantId == registration.ParticipantId && r.Status == RegistrationStatus.Waitlisted)
                    .Select(r => r.Workshop)
                    .ToListAsync(ct);
                foreach (var workshop in waitlistedElsewhere)
                    promoted.AddRange(await PromoteFromWaitlistAsync(workshop, ct));
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        await NotifyAsync(NotificationType.RegistrationCancelled, registration);
        foreach (var p in promoted)
            await NotifyAsync(NotificationType.PromotedFromWaitlist, p);

        return new CancelResult(registration, AlreadyCancelled: false, promoted);
    }

    /// <summary>
    /// Fills free seats from the waitlist in FIFO order (creation time, then id as a tie-breaker),
    /// skipping — and leaving on the waitlist — anyone who would end up with overlapping confirmed workshops.
    /// </summary>
    private async Task<List<Registration>> PromoteFromWaitlistAsync(Workshop workshop, CancellationToken ct)
    {
        var promoted = new List<Registration>();
        var freeSeats = workshop.Capacity - await CountConfirmedAsync(workshop.Id, ct);
        if (freeSeats <= 0)
            return promoted;

        var waitlist = await db.Registrations
            .Include(r => r.Participant)
            .Where(r => r.WorkshopId == workshop.Id && r.Status == RegistrationStatus.Waitlisted)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .ToListAsync(ct);

        foreach (var candidate in waitlist)
        {
            if (freeSeats == 0)
                break;
            if (await HasScheduleConflictAsync(candidate.ParticipantId, workshop, ct))
                continue;

            candidate.Status = RegistrationStatus.Confirmed;
            candidate.Workshop = workshop;
            promoted.Add(candidate);
            freeSeats--;
        }

        await db.SaveChangesAsync(ct);
        return promoted;
    }

    private Task<int> CountConfirmedAsync(int workshopId, CancellationToken ct) =>
        db.Registrations.CountAsync(r => r.WorkshopId == workshopId && r.Status == RegistrationStatus.Confirmed, ct);

    private Task<bool> HasScheduleConflictAsync(int participantId, Workshop workshop, CancellationToken ct) =>
        db.Registrations.AnyAsync(r =>
            r.ParticipantId == participantId &&
            r.Status == RegistrationStatus.Confirmed &&
            r.WorkshopId != workshop.Id &&
            r.Workshop.StartTime < workshop.EndTime &&
            workshop.StartTime < r.Workshop.EndTime, ct);

    /// <summary>
    /// Starts a transaction with BEGIN IMMEDIATE, acquiring SQLite's write lock before any reads.
    /// Concurrent writers wait (busy timeout) instead of racing on stale reads.
    /// </summary>
    private async Task<WriteTransaction> BeginWriteTransactionAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        var tx = connection.BeginTransaction(deferred: false);
        await db.Database.UseTransactionAsync(tx, ct);
        return new WriteTransaction(db, tx);
    }

    /// <summary>Commits explicitly; otherwise rolls back on dispose and detaches the transaction from EF.</summary>
    private sealed class WriteTransaction(SeatFlowDbContext db, SqliteTransaction tx) : IAsyncDisposable
    {
        public Task CommitAsync(CancellationToken ct) => tx.CommitAsync(ct);

        public async ValueTask DisposeAsync()
        {
            await db.Database.UseTransactionAsync(null);
            await tx.DisposeAsync();
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task NotifyAsync(NotificationType type, Registration r)
    {
        try
        {
            await notifications.SendAsync(new Notification(
                type, r.Id, r.WorkshopId, r.Workshop.Title, r.ParticipantId, r.Participant.Email,
                clock.GetUtcNow().UtcDateTime));
        }
        catch (Exception ex)
        {
            // The state change is already committed; a failed notification must not undo it.
            logger.LogError(ex, "Failed to send {Type} notification for registration {RegistrationId}", type, r.Id);
        }
    }
}
