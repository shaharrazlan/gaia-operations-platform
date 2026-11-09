using Gaia.Core.Errors;
using Gaia.Core.Formulas;
using Gaia.Core.Operations;

namespace Gaia.Core.Services;

public sealed record ExecutionResult(string OperationId, string? FieldA, string? FieldB, string Result);

/// <summary>
/// The business logic for end users and external apps: list the catalog, run an operation.
/// Formula operations go through the <see cref="FormulaEngine"/>; code operations through their
/// <see cref="IOperationHandler"/>. All rules live here, never in the UI (R6).
/// </summary>
public sealed class OperationService(IOperationRepository repository, IEnumerable<IOperationHandler> handlers)
{
    private readonly Dictionary<string, IOperationHandler> _handlers = handlers.ToDictionary(h => h.OperationId);

    public Task<IReadOnlyList<OperationDefinition>> ListAsync(CancellationToken cancellationToken = default) =>
        repository.ListAsync(includeDisabled: false, cancellationToken);

    public async Task<ExecutionResult> ExecuteAsync(string operationId, string? fieldA, string? fieldB,
        CancellationToken cancellationToken = default)
    {
        var operation = await repository.FindAsync(operationId, cancellationToken);
        if (operation is not { IsAvailable: true })
            throw new NotFoundException($"Operation '{operationId}' was not found.");

        var result = operation.Formula is { } formula
            ? FormulaEngine.Evaluate(formula, operation.InputType, fieldA, fieldB)
            : await RunHandlerAsync(operation, fieldA, fieldB, cancellationToken);

        return new ExecutionResult(operation.Id, fieldA, fieldB, result);
    }

    private Task<string> RunHandlerAsync(OperationDefinition operation, string? fieldA, string? fieldB,
        CancellationToken cancellationToken)
    {
        // A code operation with no handler is a deployment bug, not a user error → 500.
        if (!_handlers.TryGetValue(operation.Id, out var handler))
            throw new InvalidOperationException($"No handler is registered for code operation '{operation.Id}'.");

        var (a, b) = InputParser.Parse(operation.InputType, fieldA, fieldB);
        return handler.ExecuteAsync(a, b, cancellationToken);
    }
}
