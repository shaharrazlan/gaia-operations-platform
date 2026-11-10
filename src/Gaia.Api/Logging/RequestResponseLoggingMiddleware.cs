using System.Diagnostics;
using System.Text;

namespace Gaia.Api.Logging;

/// <summary>
/// Writes one request-log line per API request (R11.AC1): method, path, request headers and body,
/// response status and body, and duration. The CorrelationId on the line comes from <see cref="CorrelationIdMiddleware"/>
/// (R11.AC2). Secret headers such as the management key are written as "***" (R13.AC7).
/// </summary>
public sealed partial class RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
{
    /// <summary>Bodies longer than this are cut in the log (the caller still gets the whole response).</summary>
    public const int MaxBodyChars = 16 * 1024;

    private static readonly HashSet<string> SecretHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "X-Management-Key", "Authorization", "Cookie" };

    public async Task InvokeAsync(HttpContext context)
    {
        // Only the API: Swagger pages, /health and static files would drown the log.
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var started = Stopwatch.GetTimestamp();

        // Read the request body for the log, then rewind it so the controller can read it too.
        context.Request.EnableBuffering();
        var requestBody = await ReadForLogAsync(context.Request.Body);
        context.Request.Body.Position = 0;

        // Let the rest of the pipeline write into a buffer, so the response body can be logged as well.
        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = original;
            buffer.Position = 0;
            var responseBody = await ReadForLogAsync(buffer);

            LogRequest(context.Request.Method, Safe(context.Request.Path), Safe(context.Request.QueryString.Value ?? ""),
                context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Safe(HeadersForLog(context.Request.Headers)), Safe(requestBody), Safe(responseBody));

            buffer.Position = 0;
            await buffer.CopyToAsync(original, context.RequestAborted);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{Method} {Path}{Query} -> {StatusCode} in {DurationMs:0.0} ms | request headers: {RequestHeaders} | request body: {RequestBody} | response body: {ResponseBody}")]
    private partial void LogRequest(string method, string path, string query, int statusCode, double durationMs,
        string requestHeaders, string requestBody, string responseBody);

    /// <summary>
    /// Text from the caller is written escaped: a line break becomes "\n", so a request can't forge
    /// extra lines in the log file (log injection). Other control characters become \uXXXX.
    /// </summary>
    public static string Safe(string text)
    {
        if (!text.Any(char.IsControl))
            return text;
        var escaped = new StringBuilder(text.Length + 16);
        foreach (var c in text)
            escaped.Append(c switch
            {
                '\r' => @"\r",
                '\n' => @"\n",
                '\t' => @"\t",
                _ when char.IsControl(c) => $@"\u{(int)c:x4}",
                _ => c.ToString(),
            });
        return escaped.ToString();
    }

    private static string HeadersForLog(IHeaderDictionary headers) =>
        string.Join("; ", headers.Select(h => $"{h.Key}: {(SecretHeaders.Contains(h.Key) ? "***" : h.Value.ToString())}"));

    /// <summary>Reads at most <see cref="MaxBodyChars"/> characters; the stream is left for the caller to rewind.</summary>
    private static async Task<string> ReadForLogAsync(Stream body)
    {
        using var reader = new StreamReader(body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var chars = new char[MaxBodyChars + 1];
        var read = await reader.ReadBlockAsync(chars.AsMemory());
        return read > MaxBodyChars ? new string(chars, 0, MaxBodyChars) + "…(truncated)" : new string(chars, 0, read);
    }
}
