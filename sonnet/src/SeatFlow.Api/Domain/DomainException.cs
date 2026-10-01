namespace SeatFlow.Domain;

public enum DomainErrorKind { NotFound, Conflict, Validation }

public class DomainException(DomainErrorKind kind, string message) : Exception(message)
{
    public DomainErrorKind Kind { get; } = kind;
}
