namespace SeatFlow.Api.Domain;

public enum RegistrationStatus
{
    Confirmed,
    Waitlisted,
    Cancelled
}

public sealed class Workshop
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public int Capacity { get; set; }
    public List<Registration> Registrations { get; set; } = [];
}

public sealed class Participant
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public List<Registration> Registrations { get; set; } = [];
}

public sealed class Registration
{
    public int Id { get; set; }
    public int WorkshopId { get; set; }
    public Workshop? Workshop { get; set; }
    public int ParticipantId { get; set; }
    public Participant? Participant { get; set; }
    public RegistrationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class IdempotencyRecord
{
    public required string Key { get; set; }
    public int WorkshopId { get; set; }
    public int ParticipantId { get; set; }
    public int RegistrationId { get; set; }
}
