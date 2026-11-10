using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace Gaia.Api.Logging;

/// <summary>
/// Where log lines go (plan: Serilog, two files):
/// <list type="bullet">
/// <item><c>logs/errors-YYYYMMDD.log</c> — system errors only, with stack traces (R10).</item>
/// <item><c>logs/requests-YYYYMMDD.log</c> — one line per API request and its response (R11).</item>
/// <item>the console — everything at Information and above, for whoever runs the server.</item>
/// </list>
/// Every line carries the request's CorrelationId, so the two files can be matched (R11.AC2).
/// </summary>
public static class LogSetup
{
    /// <summary>How many days of log files are kept.</summary>
    public const int RetainedDays = 14;

    private const string Template =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({CorrelationId}) {Message:lj}{NewLine}{Exception}";

    /// <summary>The error log: anything logged at Error or worse (unexpected exceptions, database failures).</summary>
    public static bool IsErrorLog(LogEvent e) => e.Level >= LogEventLevel.Error;

    /// <summary>The request log: the lines written by <see cref="RequestResponseLoggingMiddleware"/>.</summary>
    public static bool IsRequestLog(LogEvent e) => Matching.FromSource<RequestResponseLoggingMiddleware>()(e);

    public static void Configure(LoggerConfiguration logger, IConfiguration configuration, IServiceProvider services, string contentRoot)
    {
        logger
            .ReadFrom.Configuration(configuration)   // minimum levels from appsettings "Serilog" section
            .ReadFrom.Services(services)             // extra sinks registered in DI (tests use an in-memory one)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: Template);

        // "LogFiles:Directory": "logs" = src/Gaia.Api/logs, whichever folder the server is started from.
        // Empty turns the files off (the tests do that).
        var setting = configuration["LogFiles:Directory"];
        if (string.IsNullOrWhiteSpace(setting))
            return;
        var directory = Path.Combine(contentRoot, setting);

        // One file per day; files older than RetainedDays are deleted (the request log holds what users typed).
        logger
            .WriteTo.Logger(errors => errors
                .Filter.ByIncludingOnly(IsErrorLog)
                .WriteTo.File(Path.Combine(directory, "errors-.log"), outputTemplate: Template,
                    rollingInterval: RollingInterval.Day, retainedFileCountLimit: RetainedDays))
            .WriteTo.Logger(requests => requests
                .Filter.ByIncludingOnly(IsRequestLog)
                .WriteTo.File(Path.Combine(directory, "requests-.log"), outputTemplate: Template,
                    rollingInterval: RollingInterval.Day, retainedFileCountLimit: RetainedDays));
    }
}
