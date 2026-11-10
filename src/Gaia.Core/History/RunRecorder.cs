using Gaia.Core.Errors;
using Microsoft.Extensions.Logging;

namespace Gaia.Core.History;

/// <summary>What a successful run returns besides its result: the 3 runs before it and this month's count (R8, R9).</summary>
public sealed record RunHistory(IReadOnlyList<OperationRun> PreviousRuns, int MonthCount);

/// <summary>
/// Stores every execution and its outcome (R7.AC1) and reads the history shown after a successful one (R8, R9).
/// History is a side effect: if the history store fails, the error is logged and the caller still gets its
/// result, just without history (R7.AC3).
/// </summary>
public sealed partial class RunRecorder(IRunRepository runs, TimeProvider time, ILogger<RunRecorder> logger)
{
    /// <summary>How many earlier runs come back with a result (R8: "the last 3").</summary>
    public const int PreviousRunCount = 3;

    /// <summary>Stores a failed run with its error. Never throws because of the history store.</summary>
    public Task RecordFailureAsync(string operationId, string? fieldA, string? fieldB, Exception error, TimeSpan duration,
        CancellationToken cancellationToken = default) =>
        TryStoreAsync(NewRun(operationId, fieldA, fieldB, null, error, duration), cancellationToken);

    /// <summary>
    /// Stores a successful run, then returns the 3 successful runs before it (A5: the current run is left out)
    /// and the number of successful runs this Israel month, the current one included (A7, A9).
    /// Returns null if the history store failed.
    /// </summary>
    public async Task<RunHistory?> RecordSuccessAsync(string operationId, string? fieldA, string? fieldB, string result,
        TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var run = NewRun(operationId, fieldA, fieldB, result, null, duration);
        if (!await TryStoreAsync(run, cancellationToken))
            return null;

        try
        {
            var previous = await runs.LatestSuccessfulAsync(operationId, run.Id, PreviousRunCount, cancellationToken);
            var monthCount = await runs.CountSuccessfulSinceAsync(operationId, IsraelMonth.StartUtc(run.CreatedAtUtc), cancellationToken);
            return new RunHistory(previous, monthCount);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogReadFailed(e, operationId);
            return null;
        }
    }

    private OperationRun NewRun(string operationId, string? fieldA, string? fieldB, string? result, Exception? error, TimeSpan duration) => new()
    {
        OperationId = operationId,
        FieldA = Clip(fieldA, OperationRun.MaxFieldLength),
        FieldB = Clip(fieldB, OperationRun.MaxFieldLength),
        Result = Clip(result, OperationRun.MaxResultLength),
        IsSuccess = error is null,
        ErrorCode = error switch { null => null, GaiaException e => e.Code, _ => ErrorCodes.Internal },
        // Expected errors keep the message the caller saw; unexpected ones keep no internals here (they go to the error log).
        ErrorMessage = error switch { null => null, GaiaException e => Clip(e.Message, OperationRun.MaxErrorMessageLength), _ => "Unexpected error." },
        CreatedAtUtc = time.GetUtcNow().UtcDateTime,
        DurationMs = (int)Math.Min(int.MaxValue, duration.TotalMilliseconds),
    };

    private async Task<bool> TryStoreAsync(OperationRun run, CancellationToken cancellationToken)
    {
        try
        {
            await runs.AddAsync(run, cancellationToken);
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogStoreFailed(e, run.OperationId);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not store the history of a run of operation '{OperationId}'. The caller still got its result.")]
    private partial void LogStoreFailed(Exception exception, string operationId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not read the history of operation '{OperationId}'. The result was returned without history.")]
    private partial void LogReadFailed(Exception exception, string operationId);

    private static string? Clip(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
