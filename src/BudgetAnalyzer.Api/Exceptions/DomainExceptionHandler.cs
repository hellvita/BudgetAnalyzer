using System.Diagnostics;
using BudgetAnalyzer.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace BudgetAnalyzer.Api.Exceptions;

public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException)
            return false;

        var (status, title) = exception switch
        {
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation"),
            _ => (StatusCodes.Status500InternalServerError, "Server Error"),
        };

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                type = "about:blank",
                title,
                status,
                detail = exception.Message,
                traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier,
            },
            cancellationToken);

        return true;
    }
}
