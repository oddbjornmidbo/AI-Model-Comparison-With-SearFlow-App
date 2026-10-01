namespace SeatFlow.Api;

public enum RegistrationStatus { Confirmed, Waitlisted, Cancelled }
public sealed class Workshop { public int Id { get; set; } public string Title { get; set; } = ""; public DateTime StartTime { get; set; } public DateTime EndTime { get; set; } public int Capacity { get; set; } public List<Registration> Registrations { get; set; } = []; }
public sealed class Participant { public int Id { get; set; } public string Name { get; set; } = ""; public string Email { get; set; } = ""; }
public sealed class Registration { public int Id { get; set; } public int WorkshopId { get; set; } public Workshop? Workshop { get; set; } public int ParticipantId { get; set; } public Participant? Participant { get; set; } public RegistrationStatus Status { get; set; } public DateTime CreatedAt { get; set; } public string? IdempotencyKey { get; set; } }
public sealed record Notification(string Kind, int RegistrationId, int ParticipantId, int WorkshopId, DateTime At);
public interface INotificationService { Task NotifyAsync(string kind, Registration registration); }
public sealed class MemoryNotificationService(ILogger<MemoryNotificationService> logger) : INotificationService
{
    public List<Notification> Sent { get; } = [];
    public Task NotifyAsync(string kind, Registration r) { lock (Sent) Sent.Add(new(kind, r.Id, r.ParticipantId, r.WorkshopId, DateTime.UtcNow)); logger.LogInformation("SeatFlow notification {Kind} for registration {RegistrationId}", kind, r.Id); return Task.CompletedTask; }
}
