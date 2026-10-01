using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Contracts;

public sealed record CreateWorkshopRequest(string? Title, DateTimeOffset StartTime, DateTimeOffset EndTime, int Capacity);
public sealed record RegisterRequest(int ParticipantId);
public sealed record WorkshopDto(int Id, string Title, DateTimeOffset StartTime, DateTimeOffset EndTime, int Capacity, int ConfirmedCount, int RemainingCapacity);
public sealed record ParticipantDto(int Id, string Name, string Email);
public sealed record RegistrationDto(int Id, int WorkshopId, int ParticipantId, string ParticipantName, string ParticipantEmail, RegistrationStatus Status, DateTimeOffset CreatedAt);
public sealed record ScheduleItemDto(int RegistrationId, int WorkshopId, string WorkshopTitle, DateTimeOffset StartTime, DateTimeOffset EndTime);

public static class ContractMapping
{
    public static WorkshopDto ToDto(this Workshop workshop, int confirmedCount) => new(
        workshop.Id, workshop.Title, workshop.StartTime, workshop.EndTime, workshop.Capacity, confirmedCount, Math.Max(0, workshop.Capacity - confirmedCount));

    public static ParticipantDto ToDto(this Participant participant) => new(participant.Id, participant.Name, participant.Email);

    public static RegistrationDto ToDto(this Registration registration) => new(
        registration.Id, registration.WorkshopId, registration.ParticipantId,
        registration.Participant?.Name ?? "Unknown participant", registration.Participant?.Email ?? "", registration.Status, registration.CreatedAt);
}
