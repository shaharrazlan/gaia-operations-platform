using System.Data.Common;
using Gaia.Core.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Api.Errors;

/// <summary>
/// Turns every exception into an RFC 7807 ProblemDetails response with a machine-readable
/// "code" (and "field" when it applies), so external apps never have to parse messages (R2.AC6).
/// Expected business errors (GaiaException) show their message; anything else shows a generic text,
/// never internal details or stack traces.
/// </summary>
public sealed class ProblemDetailsMapper(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <summary>Non-standard but widely used status for "the client went away before we answered".</summary>
    public const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // The caller disconnected (closed the tab, timed out). Not a server fault: no error body, no 500.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var problem = ToProblem(exception);
        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    public static ProblemDetails ToProblem(Exception exception)
    {
        // A failure while saving arrives wrapped: DbUpdateException → the real DbException inside.
        if (exception is DbUpdateException { InnerException: DbException inner })
            exception = inner;

        var (status, title, detail) = exception switch
        {
            ValidationException e => (StatusCodes.Status400BadRequest, "Invalid request", e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, "Operation failed", e.Message),
            ExternalServiceException { Code: ErrorCodes.ExternalServiceTimeout } e => (StatusCodes.Status504GatewayTimeout, "External service timeout", e.Message),
            ExternalServiceException e => (StatusCodes.Status502BadGateway, "External service error", e.Message),
            DbException => (StatusCodes.Status503ServiceUnavailable, "Service unavailable", "The database is not available. Please try again later."),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred."),
        };

        var code = exception switch
        {
            GaiaException e => e.Code,
            DbException => ErrorCodes.DatabaseUnavailable,
            _ => ErrorCodes.Internal,
        };

        return Problem(status, title, detail, code, (exception as GaiaException)?.Field);
    }

    /// <summary>Puts the request's id (also in the X-Correlation-Id header) in the error body, to quote when reporting a problem.</summary>
    public static void AddCorrelationId(ProblemDetails problem, HttpContext httpContext) =>
        problem.Extensions["correlationId"] = httpContext.TraceIdentifier;

    public static ProblemDetails Problem(int status, string title, string detail, string code, string? field = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;
        if (field is not null)
            problem.Extensions["field"] = field;
        return problem;
    }
}
