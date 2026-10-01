using System.Data;
using Microsoft.EntityFrameworkCore;
using SeatFlow.Api.Data;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Services;

public enum RegistrationFailure
{
    None,
    WorkshopNotFound,
    ParticipantNotFound,
    AlreadyRegistered,
    ScheduleConflict,
    IdempotencyKeyConflict,
    RegistrationNotFound
}

public sealed record RegistrationResult(Registration? Registration, RegistrationFailure Failure, bool WasReplayed = false);
public sealed record CancellationResult(Registration? Registration, Registration? Promoted, RegistrationFailure Failure, bool WasAlreadyCancelled = false);

public sealed class RegistrationService(IDbContextFactory<SeatFlowDbContext> contextFactory, INotificationService notificationService, ILogger<RegistrationService> logger)
{
    // Seat allocation and promotion are serialized across service instances in this process.
    // SQLite remains the durable source of truth; this gate avoids read-then-write races.
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    public async Task<RegistrationResult> RegisterAsync(int workshopId, int participantId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        await WriteGate.WaitAsync(cancellationToken);
        Registration? registration = null;
        var replayed = false;
        var notification = false;
        var failure = RegistrationFailure.None;
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var previousKey = await db.IdempotencyRecords.SingleOrDefaultAsync(x => x.Key == idempotencyKey, cancellationToken);
            if (previousKey is not null)
            {
                if (previousKey.WorkshopId != workshopId || previousKey.ParticipantId != participantId)
                {
                    failure = RegistrationFailure.IdempotencyKeyConflict;
                }
                else
                {
                    registration = await db.Registrations.Include(x => x.Participant)
                        .SingleOrDefaultAsync(x => x.Id == previousKey.RegistrationId, cancellationToken);
                    replayed = registration is not null;
                }
                await transaction.CommitAsync(cancellationToken);
                return new RegistrationResult(registration, failure, replayed);
            }

            var workshop = await db.Workshops.SingleOrDefaultAsync(x => x.Id == workshopId, cancellationToken);
            if (workshop is null)
                return new RegistrationResult(null, RegistrationFailure.WorkshopNotFound);
            var participant = await db.Participants.SingleOrDefaultAsync(x => x.Id == participantId, cancellationToken);
            if (participant is null)
                return new RegistrationResult(null, RegistrationFailure.ParticipantNotFound);

            var existing = await db.Registrations.AnyAsync(x => x.WorkshopId == workshopId && x.ParticipantId == participantId && x.Status != RegistrationStatus.Cancelled, cancellationToken);
            if (existing)
                return new RegistrationResult(null, RegistrationFailure.AlreadyRegistered);

            var confirmedCount = await db.Registrations.CountAsync(x => x.WorkshopId == workshopId && x.Status == RegistrationStatus.Confirmed, cancellationToken);
            var status = confirmedCount < workshop.Capacity ? RegistrationStatus.Confirmed : RegistrationStatus.Waitlisted;
            if (status == RegistrationStatus.Confirmed && await HasScheduleConflictAsync(db, participantId, workshop.StartTime, workshop.EndTime, null, cancellationToken))
                return new RegistrationResult(null, RegistrationFailure.ScheduleConflict);

            registration = new Registration
            {
                WorkshopId = workshopId,
                ParticipantId = participantId,
                Status = status,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Registrations.Add(registration);
            await db.SaveChangesAsync(cancellationToken);
            db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Key = idempotencyKey,
                WorkshopId = workshopId,
                ParticipantId = participantId,
                RegistrationId = registration.Id
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            notification = status == RegistrationStatus.Confirmed;
        }
        finally
        {
            WriteGate.Release();
        }

        if (registration is not null)
        {
            registration = await LoadRegistrationAsync(registration.Id, cancellationToken);
            if (notification)
                await NotifySafelyAsync("confirmed", registration, cancellationToken);
        }
        return new RegistrationResult(registration, failure, replayed);
    }

    public async Task<CancellationResult> CancelAsync(int registrationId, CancellationToken cancellationToken = default)
    {
        await WriteGate.WaitAsync(cancellationToken);
        Registration? cancelled = null;
        Registration? promoted = null;
        var alreadyCancelled = false;
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var registration = await db.Registrations.SingleOrDefaultAsync(x => x.Id == registrationId, cancellationToken);
            if (registration is null)
                return new CancellationResult(null, null, RegistrationFailure.RegistrationNotFound);
            if (registration.Status == RegistrationStatus.Cancelled)
            {
                alreadyCancelled = true;
                cancelled = registration;
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                var wasConfirmed = registration.Status == RegistrationStatus.Confirmed;
                registration.Status = RegistrationStatus.Cancelled;
                cancelled = registration;
                if (wasConfirmed)
                {
                    var workshop = await db.Workshops.SingleAsync(x => x.Id == registration.WorkshopId, cancellationToken);
                    var candidates = await db.Registrations
                        .Where(x => x.WorkshopId == registration.WorkshopId && x.Status == RegistrationStatus.Waitlisted)
                        .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
                        .ToListAsync(cancellationToken);
                    foreach (var candidate in candidates)
                    {
                        if (await HasScheduleConflictAsync(db, candidate.ParticipantId, workshop.StartTime, workshop.EndTime, candidate.Id, cancellationToken))
                            continue;
                        candidate.Status = RegistrationStatus.Confirmed;
                        promoted = candidate;
                        break;
                    }
                }
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            WriteGate.Release();
        }

        if (cancelled is not null)
        {
            cancelled = await LoadRegistrationAsync(cancelled.Id, cancellationToken);
            if (!alreadyCancelled)
                await NotifySafelyAsync("cancelled", cancelled, cancellationToken);
        }
        if (promoted is not null)
        {
            promoted = await LoadRegistrationAsync(promoted.Id, cancellationToken);
            await NotifySafelyAsync("promoted", promoted, cancellationToken);
        }
        return new CancellationResult(cancelled, promoted, RegistrationFailure.None, alreadyCancelled);
    }

    private static Task<bool> HasScheduleConflictAsync(SeatFlowDbContext db, int participantId, DateTimeOffset startTime, DateTimeOffset endTime, int? exceptRegistrationId, CancellationToken cancellationToken) =>
        db.Registrations.AnyAsync(x => x.ParticipantId == participantId && x.Status == RegistrationStatus.Confirmed &&
            (!exceptRegistrationId.HasValue || x.Id != exceptRegistrationId.Value) &&
            x.Workshop != null && x.Workshop.StartTime < endTime && startTime < x.Workshop.EndTime, cancellationToken);

    private async Task<Registration> LoadRegistrationAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Registrations.Include(x => x.Participant).SingleAsync(x => x.Id == id, cancellationToken);
    }

    private async Task NotifySafelyAsync(string type, Registration registration, CancellationToken cancellationToken)
    {
        try
        {
            await notificationService.SendAsync(new RegistrationNotification(type, registration.Id, registration.WorkshopId, registration.ParticipantId, DateTimeOffset.UtcNow), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Notification {Type} failed for registration {RegistrationId}; the committed operation remains successful", type, registration.Id);
        }
    }
}
