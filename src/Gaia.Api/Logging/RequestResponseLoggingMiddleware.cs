namespace Gaia.Api.Logging;

/// <summary>Writes one request-log line per API request (R11). Filled in by T48.</summary>
public sealed class RequestResponseLoggingMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context) => next(context);
}
