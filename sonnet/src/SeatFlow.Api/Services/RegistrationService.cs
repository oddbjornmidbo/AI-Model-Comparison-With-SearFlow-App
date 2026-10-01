using Microsoft.EntityFrameworkCore;
using SeatFlow.Data;
using SeatFlow.Domain;

namespace SeatFlow.Services;

public record RegisterResult(Registration Registration, bool Created);

public class RegistrationService(SeatFlowDbContext db, INotificationService notifications, ILogger<RegistrationService> logger)
{
    // All seat-affecting writes are serialised through this lock, then run in a transaction.
    // SQLite allows a single writer anyway; the lock makes the check-then-insert sequence atomic
    // for every request in this process. (Multi-instance deployments would need a different strategy.)
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    public async Task<RegisterResult> RegisterAsync(int workshopId, int participantId, string? idempotencyKey, CancellationToken ct = default)
    {
        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (idempotencyKey is { Length: > 200 })
            throw new DomainException(DomainErrorKind.Validation, "Idempotency-Key must be at most 200 characters.");

        Notification? toSend = null;
        RegisterResult result;

        await WriteLock.WaitAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            if (idempotencyKey != null)
            {
                var prior = await db.IdempotencyRecords.FirstOrDefaultAsync(r => r.Key == idempotencyKey, ct);
                if (prior != null)
                {
                    if (prior.WorkshopId != workshopId || prior.ParticipantId != participantId)
                        throw new DomainException(DomainErrorKind.Conflict, "Idempotency-Key was already used for a different request.");
                    var existing = await LoadRegistration(prior.RegistrationId, ct);
                    return new RegisterResult(existing, Created: false);
                }
            }

            var workshop = await db.Workshops.FindAsync([workshopId], ct)
                ?? throw new DomainException(DomainErrorKind.NotFound, $"Workshop {workshopId} not found.");
            var participant = await db.Participants.FindAsync([participantId], ct)
                ?? throw new DomainException(DomainErrorKind.NotFound, $"Participant {participantId} not found.");

            if (await db.Registrations.AnyAsync(r => r.WorkshopId == workshopId && r.ParticipantId == participantId
                                                     && r.Status != RegistrationStatus.Cancelled, ct))
                throw new DomainException(DomainErrorKind.Conflict, "Participant already has an active registration for this workshop.");

            var confirmedCount = await db.Registrations.CountAsync(r => r.WorkshopId == workshopId && r.Status == RegistrationStatus.Confirmed, ct);
            var status = RegistrationStatus.Waitlisted;
            if (confirmedCount < workshop.Capacity)
            {
                if (await HasConfirmedConflict(participantId, workshop, ct))
                    throw new DomainException(DomainErrorKind.Conflict, "Participant is already confirmed for an overlapping workshop.");
                status = RegistrationStatus.Confirmed;
            }

            var registration = new Registration
            {
                WorkshopId = workshopId, ParticipantId = participantId,
                Status = status, CreatedAt = DateTime.UtcNow,
            };
            db.Registrations.Add(registration);
            await db.SaveChangesAsync(ct);

            if (idempotencyKey != null)
            {
                db.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    Key = idempotencyKey, WorkshopId = workshopId, ParticipantId = participantId, RegistrationId = registration.Id,
                });
                await db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);

            registration.Workshop = workshop;
            registration.Participant = participant;
            result = new RegisterResult(registration, Created: true);
            if (status == RegistrationStatus.Confirmed)
                toSend = new Notification(NotificationType.RegistrationConfirmed, registration.Id, workshopId, participantId,
                    $"{participant.Name}: your seat in '{workshop.Title}' is confirmed.");
        }
        finally
        {
            WriteLock.Release();
        }

        await TryNotify(toSend);
        return result;
    }

    public async Task<Registration> CancelAsync(int registrationId, CancellationToken ct = default)
    {
        var toSend = new List<Notification>();
        Registration registration;

        await WriteLock.WaitAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            registration = await LoadRegistration(registrationId, ct);
            if (registration.Status == RegistrationStatus.Cancelled)
                return registration; // idempotent: nothing changes, nothing is sent

            var wasConfirmed = registration.Status == RegistrationStatus.Confirmed;
            registration.Status = RegistrationStatus.Cancelled;
            toSend.Add(new Notification(NotificationType.RegistrationCancelled, registration.Id, registration.WorkshopId, registration.ParticipantId,
                $"{registration.Participant.Name}: your registration for '{registration.Workshop.Title}' was cancelled."));

            if (wasConfirmed)
            {
                var promoted = await PromoteNextEligible(registration.Workshop, ct);
                if (promoted != null)
                    toSend.Add(new Notification(NotificationType.WaitlistPromoted, promoted.Id, promoted.WorkshopId, promoted.ParticipantId,
                        $"{promoted.Participant.Name}: you were promoted from the waitlist and are now confirmed for '{registration.Workshop.Title}'."));
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        finally
        {
            WriteLock.Release();
        }

        foreach (var n in toSend) await TryNotify(n);
        return registration;
    }

    /// <summary>Promotes the oldest waitlisted registration whose participant has no overlapping confirmed workshop.</summary>
    private async Task<Registration?> PromoteNextEligible(Workshop workshop, CancellationToken ct)
    {
        var waitlist = await db.Registrations.Include(r => r.Participant)
            .Where(r => r.WorkshopId == workshop.Id && r.Status == RegistrationStatus.Waitlisted)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .ToListAsync(ct);

        foreach (var candidate in waitlist)
        {
            if (await HasConfirmedConflict(candidate.ParticipantId, workshop, ct)) continue; // stays on the waitlist
            candidate.Status = RegistrationStatus.Confirmed;
            return candidate;
        }
        return null;
    }

    private async Task<bool> HasConfirmedConflict(int participantId, Workshop workshop, CancellationToken ct)
    {
        var others = await db.Registrations
            .Where(r => r.ParticipantId == participantId && r.Status == RegistrationStatus.Confirmed && r.WorkshopId != workshop.Id)
            .Select(r => r.Workshop)
            .ToListAsync(ct);
        return others.Any(workshop.OverlapsWith);
    }

    private async Task<Registration> LoadRegistration(int id, CancellationToken ct) =>
        await db.Registrations.Include(r => r.Workshop).Include(r => r.Participant).FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new DomainException(DomainErrorKind.NotFound, $"Registration {id} not found.");

    // Notification failures must never undo a committed registration/cancellation.
    private async Task TryNotify(Notification? n)
    {
        if (n == null) return;
        try { await notifications.SendAsync(n); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to send {Type} notification for registration {Id}", n.Type, n.RegistrationId); }
    }
}
