namespace SeatFlow.Api.Domain;

public class Workshop
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }

    public List<Registration> Registrations { get; set; } = [];
}

public class Participant
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
}

public enum RegistrationStatus
{
    Confirmed,
    Waitlisted,
    Cancelled,
}

public class Registration
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop Workshop { get; set; } = null!;
    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    public RegistrationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>Client-supplied Idempotency-Key used when the registration was created (unique when present).</summary>
    public string? IdempotencyKey { get; set; }
}
