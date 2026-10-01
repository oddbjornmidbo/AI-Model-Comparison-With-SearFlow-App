namespace SeatFlow.Services;

public enum NotificationType { RegistrationConfirmed, WaitlistPromoted, RegistrationCancelled }

public record Notification(NotificationType Type, int RegistrationId, int WorkshopId, int ParticipantId, string Message);

public interface INotificationService
{
    Task SendAsync(Notification notification, CancellationToken ct = default);
}
