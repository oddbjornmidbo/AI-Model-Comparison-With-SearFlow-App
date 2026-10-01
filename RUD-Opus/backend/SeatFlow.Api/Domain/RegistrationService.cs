using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SeatFlow.Api.Data;
using SeatFlow.Api.Notifications;

namespace SeatFlow.Api.Domain;

public sealed record RegisterResult(Registration Registration, bool Replayed);

public sealed record CancelResult(Registration Registration, bool AlreadyCancelled, IReadOnlyList<Registration> Promoted);

/// <summary>
/// Registration commands. Every command runs inside a SQLite write transaction (BEGIN IMMEDIATE), which takes the
/// database write lock before anything is read. Capacity checks, overlap checks and idempotency lookups are therefore
/// serialized across all requests and processes, so two requests can never both see "one seat left".
/// Notifications are sent only after commit and their failures are logged, never propagated.
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
                var previous = await db.IdempotencyRecords.FindAsync([idempotencyKey], ct);
                if (previous is not null)
                {
                    if (previous.WorkshopId != workshopId || previous.ParticipantId != participantId)
                    {
                        throw BusinessRuleException.Unprocessable("idempotency_key_reused",
                            "This Idempotency-Key was already used for a different registration request.");
                    }

                    var existing = await LoadAsync(previous.RegistrationId, ct);
                    return new RegisterResult(existing, Replayed: true);
                }
            }

            var workshop = await db.Workshops.FindAsync([workshopId], ct)
                ?? throw BusinessRuleException.NotFound($"Workshop {workshopId} was not found.");
            var participant = await db.Participants.FindAsync([participantId], ct)
                ?? throw BusinessRuleException.NotFound($"Participant {participantId} was not found.");

            var alreadyActive = await db.Registrations.AnyAsync(r =>
                r.WorkshopId == workshopId && r.ParticipantId == participantId && r.Status != RegistrationStatus.Cancelled, ct);
            if (alreadyActive)
            {
                throw BusinessRuleException.Conflict("duplicate_registration",
                    $"{participant.Name} is already registered for '{workshop.Title}'.");
            }

            RegistrationStatus status;
            if (await ConfirmedCountAsync(workshopId, ct) < workshop.Capacity)
            {
                var conflict = await FindConfirmedConflictAsync(participantId, workshop, ct);
                if (conflict is not null)
                {
                    throw BusinessRuleException.Conflict("schedule_conflict",
                        $"{participant.Name} is already confirmed for '{conflict.Title}', which overlaps '{workshop.Title}'.");
                }
                status = RegistrationStatus.Confirmed;
            }
            else
            {
                // A full workshop always accepts waitlist entries; schedule conflicts are re-checked on promotion.
                status = RegistrationStatus.Waitlisted;
            }

            var now = clock.GetUtcNow().UtcDateTime;
            registration = new Registration
            {
                Workshop = workshop,
                Participant = participant,
                Status = status,
                CreatedAt = now,
            };
            db.Registrations.Add(registration);
            await db.SaveChangesAsync(ct);

            if (idempotencyKey is not null)
            {
                db.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    Key = idempotencyKey,
                    WorkshopId = workshopId,
                    ParticipantId = participantId,
                    RegistrationId = registration.Id,
                    CreatedAt = now,
                });
                await db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        }

        if (registration.Status == RegistrationStatus.Confirmed)
        {
            await NotifyAsync(NotificationType.RegistrationConfirmed, registration);
        }

        return new RegisterResult(registration, Replayed: false);
    }

    public async Task<CancelResult> CancelAsync(int registrationId, CancellationToken ct = default)
    {
        Registration registration;
        var promoted = new List<Registration>();
        await using (var tx = await BeginWriteTransactionAsync(ct))
        {
            registration = await LoadAsync(registrationId, ct);
            if (registration.Status == RegistrationStatus.Cancelled)
            {
                return new CancelResult(registration, AlreadyCancelled: true, Promoted: []);
            }

            var wasConfirmed = registration.Status == RegistrationStatus.Confirmed;
            registration.Status = RegistrationStatus.Cancelled;
            registration.CancelledAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);

            if (wasConfirmed)
            {
                // The freed seat goes to the oldest eligible waitlisted registration.
                promoted.AddRange(await FillFreeSeatsAsync(registration.WorkshopId, ct));

                // The participant's schedule just got a gap, so their own waitlisted registrations elsewhere may have
                // become eligible for seats that were left free because of an earlier conflict.
                var otherWaitlistedWorkshops = await db.Registrations
                    .Where(r => r.ParticipantId == registration.ParticipantId && r.Status == RegistrationStatus.Waitlisted)
                    .Select(r => r.WorkshopId)
                    .ToListAsync(ct);
                foreach (var workshopId in otherWaitlistedWorkshops)
                {
                    promoted.AddRange(await FillFreeSeatsAsync(workshopId, ct));
                }
            }

            await tx.CommitAsync(ct);
        }

        await NotifyAsync(NotificationType.RegistrationCancelled, registration);
        foreach (var p in promoted)
        {
            await NotifyAsync(NotificationType.PromotedFromWaitlist, p);
        }

        return new CancelResult(registration, AlreadyCancelled: false, promoted);
    }

    /// <summary>
    /// Promotes waitlisted registrations in FIFO order while seats are free, skipping (and leaving on the waitlist)
    /// participants who would get overlapping confirmed workshops. Must run inside a write transaction.
    /// </summary>
    private async Task<List<Registration>> FillFreeSeatsAsync(int workshopId, CancellationToken ct)
    {
        var promoted = new List<Registration>();
        var workshop = await db.Workshops.SingleAsync(w => w.Id == workshopId, ct);
        var freeSeats = workshop.Capacity - await ConfirmedCountAsync(workshopId, ct);
        if (freeSeats <= 0)
        {
            return promoted;
        }

        var waitlist = await db.Registrations
            .Include(r => r.Participant)
            .Where(r => r.WorkshopId == workshopId && r.Status == RegistrationStatus.Waitlisted)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .ToListAsync(ct);

        foreach (var candidate in waitlist)
        {
            if (freeSeats == 0)
            {
                break;
            }

            if (await FindConfirmedConflictAsync(candidate.ParticipantId, workshop, ct) is not null)
            {
                continue;
            }

            candidate.Status = RegistrationStatus.Confirmed;
            candidate.PromotedAt = clock.GetUtcNow().UtcDateTime;
            // Saved immediately so later conflict checks (in this or another workshop) see the new confirmation.
            await db.SaveChangesAsync(ct);
            promoted.Add(candidate);
            freeSeats--;
        }

        return promoted;
    }

    private Task<int> ConfirmedCountAsync(int workshopId, CancellationToken ct) =>
        db.Registrations.CountAsync(r => r.WorkshopId == workshopId && r.Status == RegistrationStatus.Confirmed, ct);

    private Task<Workshop?> FindConfirmedConflictAsync(int participantId, Workshop target, CancellationToken ct) =>
        db.Registrations
            .Where(r => r.ParticipantId == participantId
                && r.Status == RegistrationStatus.Confirmed
                && r.WorkshopId != target.Id
                && r.Workshop.StartTime < target.EndTime
                && target.StartTime < r.Workshop.EndTime)
            .Select(r => r.Workshop)
            .FirstOrDefaultAsync(ct);

    private async Task<Registration> LoadAsync(int registrationId, CancellationToken ct) =>
        await db.Registrations
            .Include(r => r.Workshop)
            .Include(r => r.Participant)
            .SingleOrDefaultAsync(r => r.Id == registrationId, ct)
        ?? throw BusinessRuleException.NotFound($"Registration {registrationId} was not found.");

    /// <summary>
    /// Starts BEGIN IMMEDIATE explicitly (not a deferred transaction), so the write lock is held before the first read.
    /// Waiting writers are retried by Microsoft.Data.Sqlite until the connection's Default Timeout.
    /// </summary>
    private async Task<IDbContextTransaction> BeginWriteTransactionAsync(CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        var transaction = connection.BeginTransaction(deferred: false);
        return await db.Database.UseTransactionAsync(transaction, ct)
            ?? throw new InvalidOperationException("Could not enlist the SQLite transaction.");
    }

    private async Task NotifyAsync(NotificationType type, Registration registration)
    {
        try
        {
            await notifications.SendAsync(new Notification(
                type,
                registration.Id,
                registration.WorkshopId,
                registration.Workshop.Title,
                registration.ParticipantId,
                registration.Participant.Email,
                clock.GetUtcNow().UtcDateTime));
        }
        catch (Exception ex)
        {
            // The registration change is already committed; a failed notification must not undo it.
            logger.LogError(ex, "Failed to send {Type} notification for registration {RegistrationId}", type, registration.Id);
        }
    }
}
