using Microsoft.AspNetCore.Diagnostics;
using SeatFlow.Api.Domain;

namespace SeatFlow.Api.Api;

/// <summary>Turns <see cref="BusinessRuleException"/> into RFC 7807 problem responses with a machine-readable code.</summary>
public sealed class ProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not BusinessRuleException rule) return false;

        httpContext.Response.StatusCode = rule.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = rule.StatusCode,
                Title = rule.StatusCode switch
                {
                    StatusCodes.Status404NotFound => "Not Found",
                    StatusCodes.Status409Conflict => "Conflict",
                    _ => "Unprocessable Request",
                },
                Detail = rule.Message,
                Extensions = { ["code"] = rule.Code },
            },
        });
    }
}
