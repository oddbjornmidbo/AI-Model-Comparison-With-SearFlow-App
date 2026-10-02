using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Api;

public sealed record WorkshopDto(
    int Id, string Title, DateTime StartTime, DateTime EndTime, int Capacity,
    int ConfirmedCount, int WaitlistedCount, int RemainingSeats);

public sealed record ParticipantDto(int Id, string Name, string Email);

public sealed record RegistrationDto(
    int Id, int WorkshopId, int ParticipantId, string ParticipantName, string ParticipantEmail,
    RegistrationStatus Status, DateTime CreatedAt, int? WaitlistPosition);

public sealed record WorkshopSummaryDto(int Id, string Title, DateTime StartTime, DateTime EndTime);

public sealed record ScheduleItemDto(
    int RegistrationId, RegistrationStatus Status, DateTime CreatedAt, int? WaitlistPosition, WorkshopSummaryDto Workshop);

public sealed record ScheduleDto(ParticipantDto Participant, IReadOnlyList<ScheduleItemDto> Registrations);

public sealed record CreateWorkshopRequest(string? Title, DateTimeOffset? StartTime, DateTimeOffset? EndTime, int? Capacity);

public sealed record CreateParticipantRequest(string? Name, string? Email);

public sealed record CreateRegistrationRequest(int? ParticipantId);
