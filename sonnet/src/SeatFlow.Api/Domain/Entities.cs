namespace SeatFlow.Domain;

public enum RegistrationStatus { Confirmed, Waitlisted, Cancelled }

public class Workshop
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartTime { get; set; }   // UTC
    public DateTime EndTime { get; set; }     // UTC
    public int Capacity { get; set; }

    public bool OverlapsWith(Workshop other) => StartTime < other.EndTime && other.StartTime < EndTime;
}

public class Participant
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}

public class Registration
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public int ParticipantId { get; set; }
    public RegistrationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }   // UTC

    public Workshop Workshop { get; set; } = null!;
    public Participant Participant { get; set; } = null!;
}

/// <summary>Remembers which registration an Idempotency-Key produced.</summary>
public class IdempotencyRecord
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public int WorkshopId { get; set; }
    public int ParticipantId { get; set; }
    public int RegistrationId { get; set; }
}
