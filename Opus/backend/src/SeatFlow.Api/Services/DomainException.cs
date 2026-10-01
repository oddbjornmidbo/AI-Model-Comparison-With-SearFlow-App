namespace SeatFlow.Api.Services;

public enum DomainError
{
    NotFound,
    AlreadyRegistered,
    ScheduleConflict,
    IdempotencyKeyReused,
}

public sealed class DomainException(DomainError error, string message) : Exception(message)
{
    public DomainError Error { get; } = error;
}
