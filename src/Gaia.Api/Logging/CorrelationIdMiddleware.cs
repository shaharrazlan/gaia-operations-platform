using System.Text.RegularExpressions;
using Serilog.Context;

namespace Gaia.Api.Logging;

/// <summary>
/// Gives every request an id, returned in the <c>X-Correlation-Id</c> header and attached to every log line
/// written while handling it — so a request in the request log can be matched with its error-log entry (R11.AC2).
/// A caller may send its own id (to trace a call across systems); anything unsafe is replaced.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    // Short and plain, so a caller can't inject odd text into the logs.
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex SafeId();

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        var id = SafeId().IsMatch(supplied) ? supplied : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = id;   // also used for "correlationId" in error bodies
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", id))
            await next(context);
    }
}
