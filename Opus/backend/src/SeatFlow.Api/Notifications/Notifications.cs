using System.Collections.Concurrent;

namespace SeatFlow.Api.Notifications;

public enum NotificationType
{
    RegistrationConfirmed,
    PromotedFromWaitlist,
    RegistrationCancelled,
}

public sealed record Notification(
    NotificationType Type,
    int RegistrationId,
    int WorkshopId,
    string WorkshopTitle,
    int ParticipantId,
    string ParticipantEmail,
    DateTime CreatedAt);

public interface INotificationService
{
    Task SendAsync(Notification notification, CancellationToken cancellationToken = default);
}

/// <summary>Development implementation: keeps sent notifications in memory and logs them.</summary>
public sealed class InMemoryNotificationService(ILogger<InMemoryNotificationService> logger) : INotificationService
{
    private const int MaxKept = 500;
    private readonly ConcurrentQueue<Notification> _sent = new();

    public IReadOnlyList<Notification> Sent => _sent.ToArray();

    public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(notification);
        while (_sent.Count > MaxKept && _sent.TryDequeue(out _)) { }

        logger.LogInformation(
            "Notification {Type} to {Email}: registration {RegistrationId} for '{Workshop}'",
            notification.Type, notification.ParticipantEmail, notification.RegistrationId, notification.WorkshopTitle);
        return Task.CompletedTask;
    }
}
