using Gaia.Core.Errors;
using Microsoft.Extensions.Logging;

namespace Gaia.Core.History;

/// <summary>
/// Stores every execution and its outcome (R7.AC1). History is a side effect: if storing fails, the error
/// is logged and the caller still gets its result (R7.AC3, A9).
/// </summary>
public sealed partial class RunRecorder(IRunRepository runs, TimeProvider time, ILogger<RunRecorder> logger)
{
    /// <summary>
    /// Stores one run: a result for a success, the error for a failure. Returns the stored run,
    /// or null if the history store failed (that failure is logged, never thrown).
    /// </summary>
    public async Task<OperationRun?> RecordAsync(string operationId, string? fieldA, string? fieldB, string? result,
        Exception? error, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var run = new OperationRun
        {
            OperationId = operationId,
            FieldA = Clip(fieldA, OperationRun.MaxFieldLength),
            FieldB = Clip(fieldB, OperationRun.MaxFieldLength),
            Result = error is null ? Clip(result, OperationRun.MaxResultLength) : null,
            IsSuccess = error is null,
            ErrorCode = error switch { null => null, GaiaException e => e.Code, _ => ErrorCodes.Internal },
            // Expected errors keep the message the caller saw; unexpected ones keep no internals here (they go to the error log).
            ErrorMessage = error switch { null => null, GaiaException e => Clip(e.Message, OperationRun.MaxErrorMessageLength), _ => "Unexpected error." },
            CreatedAtUtc = time.GetUtcNow().UtcDateTime,
            DurationMs = (int)Math.Min(int.MaxValue, duration.TotalMilliseconds),
        };

        try
        {
            await runs.AddAsync(run, cancellationToken);
            return run;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogStoreFailed(e, operationId);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not store the history of a run of operation '{OperationId}'. The caller still got its result.")]
    private partial void LogStoreFailed(Exception exception, string operationId);

    private static string? Clip(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
