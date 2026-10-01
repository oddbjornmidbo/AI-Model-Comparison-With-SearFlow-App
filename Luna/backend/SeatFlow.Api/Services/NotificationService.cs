using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Services;

public sealed record RegistrationNotification(string Type, int RegistrationId, int WorkshopId, int ParticipantId, DateTimeOffset At);

public interface INotificationService
{
    Task SendAsync(RegistrationNotification notification, CancellationToken cancellationToken = default);
}

public sealed class DevelopmentNotificationService(ILogger<DevelopmentNotificationService> logger) : INotificationService
{
    private readonly ConcurrentQueue<RegistrationNotification> _notifications = new();

    public IReadOnlyCollection<RegistrationNotification> Notifications => _notifications.ToArray();

    public Task SendAsync(RegistrationNotification notification, CancellationToken cancellationToken = default)
    {
        _notifications.Enqueue(notification);
        logger.LogInformation("SeatFlow notification {Type}: registration {RegistrationId}, workshop {WorkshopId}, participant {ParticipantId}",
            notification.Type, notification.RegistrationId, notification.WorkshopId, notification.ParticipantId);
        return Task.CompletedTask;
    }
}
