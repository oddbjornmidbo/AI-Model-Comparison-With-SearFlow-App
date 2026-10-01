using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Endpoints;

public sealed record WorkshopDto(
    int Id,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    int Capacity,
    int ConfirmedCount,
    int WaitlistCount,
    int RemainingCapacity);

public sealed record CreateWorkshopRequest(string? Title, DateTime? StartTime, DateTime? EndTime, int? Capacity);

public sealed record ParticipantDto(int Id, string Name, string Email);

public sealed record CreateRegistrationRequest(int? ParticipantId);

public sealed record RegistrationDto(
    int Id,
    int WorkshopId,
    int ParticipantId,
    string ParticipantName,
    string ParticipantEmail,
    RegistrationStatus Status,
    DateTime CreatedAt,
    DateTime? CancelledAt,
    int? WaitlistPosition)
{
    public static RegistrationDto From(Registration r, int? waitlistPosition = null) => new(
        r.Id, r.WorkshopId, r.ParticipantId, r.Participant.Name, r.Participant.Email,
        r.Status, r.CreatedAt, r.CancelledAt, waitlistPosition);
}

public sealed record CancelResponse(RegistrationDto Registration, bool AlreadyCancelled, IReadOnlyList<RegistrationDto> Promoted);

public sealed record ScheduleItemDto(
    int RegistrationId,
    RegistrationStatus Status,
    int WorkshopId,
    string WorkshopTitle,
    DateTime StartTime,
    DateTime EndTime,
    DateTime CreatedAt,
    int? WaitlistPosition);

public sealed record ScheduleDto(ParticipantDto Participant, IReadOnlyList<ScheduleItemDto> Items);
