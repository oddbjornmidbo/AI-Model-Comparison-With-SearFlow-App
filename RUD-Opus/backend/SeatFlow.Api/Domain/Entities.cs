namespace SeatFlow.Api.Domain;

public enum RegistrationStatus
{
    Confirmed,
    Waitlisted,
    Cancelled,
}

public class Workshop
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; }

    public List<Registration> Registrations { get; set; } = [];

    /// <summary>Half-open intervals: a workshop ending at 10:00 does not overlap one starting at 10:00.</summary>
    public bool Overlaps(Workshop other) => StartTime < other.EndTime && other.StartTime < EndTime;
}

public class Participant
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
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
    public DateTime? PromotedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

/// <summary>Remembers which registration a given Idempotency-Key produced.</summary>
public class IdempotencyRecord
{
    public required string Key { get; set; }
    public int WorkshopId { get; set; }
    public int ParticipantId { get; set; }
    public int RegistrationId { get; set; }
    public DateTime CreatedAt { get; set; }
}
