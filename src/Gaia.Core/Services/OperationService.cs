using System.Diagnostics;
using Gaia.Core.Errors;
using Gaia.Core.Formulas;
using Gaia.Core.History;
using Gaia.Core.Operations;

namespace Gaia.Core.Services;

/// <summary>The result of a run, with its history (null if the history store failed — R7.AC3).</summary>
public sealed record ExecutionResult(string OperationId, string? FieldA, string? FieldB, string Result, RunHistory? History);

/// <summary>
/// The business logic for end users and external apps: list the catalog, run an operation.
/// Formula operations go through the <see cref="FormulaEngine"/>; code operations through their
/// <see cref="IOperationHandler"/>. Every run of an existing operation is recorded (R7). All rules live here, never in the UI (R6).
/// </summary>
public sealed class OperationService(IOperationRepository repository, IEnumerable<IOperationHandler> handlers, RunRecorder recorder)
{
    public Task<IReadOnlyList<OperationDefinition>> ListAsync(CancellationToken cancellationToken = default) =>
        repository.ListAsync(includeDisabled: false, cancellationToken);

    public async Task<ExecutionResult> ExecuteAsync(string operationId, string? fieldA, string? fieldB,
        CancellationToken cancellationToken = default)
    {
        var operation = await repository.FindAsync(operationId, cancellationToken);
        if (operation is not { IsAvailable: true })
            throw new NotFoundException($"Operation '{operationId}' was not found.");   // nothing to record: no such operation

        var started = Stopwatch.GetTimestamp();
        string result;
        try
        {
            result = operation.Formula is { } formula
                ? FormulaEngine.Evaluate(formula, operation.InputType, fieldA, fieldB)
                : await RunHandlerAsync(operation, fieldA, fieldB, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // A failed run is history too (R7.AC1); the caller still gets the original error.
            await recorder.RecordFailureAsync(operation.Id, fieldA, fieldB, error, Stopwatch.GetElapsedTime(started));
            throw;
        }

        var history = await recorder.RecordSuccessAsync(operation.Id, fieldA, fieldB, result, Stopwatch.GetElapsedTime(started), cancellationToken);
        return new ExecutionResult(operation.Id, fieldA, fieldB, result, history);
    }

    private Task<string> RunHandlerAsync(OperationDefinition operation, string? fieldA, string? fieldB,
        CancellationToken cancellationToken)
    {
        // A code operation with no handler is a deployment bug, not a user error → 500.
        // (Missing and duplicate handlers are also caught at startup by HandlerCheck.)
        var handler = handlers.FirstOrDefault(h => h.OperationId == operation.Id)
            ?? throw new InvalidOperationException($"No handler is registered for code operation '{operation.Id}'.");

        var (a, b) = InputParser.Parse(operation.InputType, fieldA, fieldB);
        return handler.ExecuteAsync(a, b, cancellationToken);
    }
}
