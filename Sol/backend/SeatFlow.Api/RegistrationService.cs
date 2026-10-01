using Microsoft.EntityFrameworkCore;
namespace SeatFlow.Api;

public sealed class RegistrationService(SeatFlowDb db, INotificationService notifications, ILogger<RegistrationService> logger)
{
    // SQLite permits one writer at a time. This gate keeps local requests ordered while
    // each transaction reads capacity and writes the new state.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private Task<bool> HasConflict(int participantId, Workshop workshop) => db.Registrations
        .AnyAsync(r => r.ParticipantId == participantId && r.Status == RegistrationStatus.Confirmed &&
            r.WorkshopId != workshop.Id && r.Workshop!.StartTime < workshop.EndTime &&
            r.Workshop.EndTime > workshop.StartTime);

    public async Task<Registration> Register(int workshopId, int participantId, string? key)
    {
        await Gate.WaitAsync();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var workshop = await db.Workshops.SingleOrDefaultAsync(w => w.Id == workshopId)
                ?? throw new KeyNotFoundException("Workshop not found");
            if (!await db.Participants.AnyAsync(p => p.Id == participantId))
                throw new KeyNotFoundException("Participant not found");
            if (!string.IsNullOrWhiteSpace(key))
            {
                var prior = await db.Registrations.FirstOrDefaultAsync(r => r.WorkshopId == workshopId && r.IdempotencyKey == key);
                if (prior != null)
                {
                    if (prior.ParticipantId != participantId) throw new InvalidOperationException("Idempotency key was used for a different participant");
                    return prior;
                }
            }
            if (await db.Registrations.AnyAsync(r => r.WorkshopId == workshopId && r.ParticipantId == participantId && r.Status != RegistrationStatus.Cancelled))
                throw new InvalidOperationException("Participant is already registered for this workshop");
            var confirmed = await db.Registrations.CountAsync(r => r.WorkshopId == workshopId && r.Status == RegistrationStatus.Confirmed);
            if (confirmed < workshop.Capacity && await HasConflict(participantId, workshop))
                throw new InvalidOperationException("Participant has an overlapping confirmed workshop");
            var registration = new Registration
            {
                WorkshopId = workshopId, ParticipantId = participantId, CreatedAt = DateTime.UtcNow,
                Status = confirmed < workshop.Capacity ? RegistrationStatus.Confirmed : RegistrationStatus.Waitlisted,
                IdempotencyKey = string.IsNullOrWhiteSpace(key) ? null : key
            };
            db.Registrations.Add(registration);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            if (registration.Status == RegistrationStatus.Confirmed) await NotifySafely("Confirmed", registration);
            return registration;
        }
        finally { Gate.Release(); }
    }

    public async Task<Registration> Cancel(int id)
    {
        await Gate.WaitAsync();
        try
        {
            Registration? promoted = null;
            await using var transaction = await db.Database.BeginTransactionAsync();
            var registration = await db.Registrations.Include(r => r.Workshop).SingleOrDefaultAsync(r => r.Id == id)
                ?? throw new KeyNotFoundException("Registration not found");
            if (registration.Status == RegistrationStatus.Cancelled) return registration;
            var wasConfirmed = registration.Status == RegistrationStatus.Confirmed;
            registration.Status = RegistrationStatus.Cancelled;
            if (wasConfirmed && registration.Workshop != null)
            {
                var queue = await db.Registrations.Where(r => r.WorkshopId == registration.WorkshopId && r.Status == RegistrationStatus.Waitlisted)
                    .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToListAsync();
                foreach (var candidate in queue)
                {
                    if (await HasConflict(candidate.ParticipantId, registration.Workshop)) continue;
                    candidate.Status = RegistrationStatus.Confirmed;
                    promoted = candidate;
                    break;
                }
            }
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            await NotifySafely("Cancelled", registration);
            if (promoted != null) await NotifySafely("Promoted", promoted);
            return registration;
        }
        finally { Gate.Release(); }
    }

    private async Task NotifySafely(string kind, Registration registration)
    {
        try { await notifications.NotifyAsync(kind, registration); }
        catch (Exception ex) { logger.LogWarning(ex, "Notification failed for registration {RegistrationId}", registration.Id); }
    }
}
