using System.Collections.Concurrent;

namespace SeatFlow.Services;

/// <summary>Development implementation: logs notifications and keeps them in memory.</summary>
public class InMemoryNotificationService(ILogger<InMemoryNotificationService> logger) : INotificationService
{
    private readonly ConcurrentQueue<Notification> _sent = new();

    public IReadOnlyCollection<Notification> Sent => _sent.ToArray();

    public Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        _sent.Enqueue(notification);
        logger.LogInformation("Notification {Type} to participant {ParticipantId}: {Message}",
            notification.Type, notification.ParticipantId, notification.Message);
        return Task.CompletedTask;
    }
}
