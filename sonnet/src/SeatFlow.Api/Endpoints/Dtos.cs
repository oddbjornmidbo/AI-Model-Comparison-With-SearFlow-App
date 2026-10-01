using SeatFlow.Domain;

namespace SeatFlow.Endpoints;

public record CreateWorkshopRequest(string? Title, DateTime? StartTime, DateTime? EndTime, int? Capacity);
public record CreateParticipantRequest(string? Name, string? Email);
public record RegisterRequest(int? ParticipantId);

public record WorkshopDto(int Id, string Title, DateTime StartTime, DateTime EndTime, int Capacity,
    int ConfirmedCount, int WaitlistedCount, int RemainingCapacity);

public record ParticipantDto(int Id, string Name, string Email);

public record RegistrationDto(int Id, int WorkshopId, string WorkshopTitle, DateTime WorkshopStart, DateTime WorkshopEnd,
    int ParticipantId, string ParticipantName, string ParticipantEmail, string Status, DateTime CreatedAt)
{
    public static RegistrationDto From(Registration r) => new(r.Id, r.WorkshopId, r.Workshop.Title, r.Workshop.StartTime, r.Workshop.EndTime,
        r.ParticipantId, r.Participant.Name, r.Participant.Email, r.Status.ToString(), r.CreatedAt);
}
