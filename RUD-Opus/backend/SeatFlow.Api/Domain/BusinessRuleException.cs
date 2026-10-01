namespace SeatFlow.Api.Domain;

/// <summary>A request that is well-formed but violates a business rule or refers to missing data.</summary>
public sealed class BusinessRuleException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;

    public static BusinessRuleException NotFound(string message) => new(StatusCodes.Status404NotFound, "not_found", message);
    public static BusinessRuleException Conflict(string code, string message) => new(StatusCodes.Status409Conflict, code, message);
    public static BusinessRuleException Unprocessable(string code, string message) => new(StatusCodes.Status422UnprocessableEntity, code, message);
}
