using Gaia.Core.Formulas;

namespace Gaia.Core.Operations;

/// <summary>
/// A code-backed operation, for what a formula can't do (e.g. calling an external weather service).
/// Implement this, register it in DI, and give the operation row the same id and no formula.
/// The inputs arrive already parsed and validated by the operation's InputType.
/// </summary>
public interface IOperationHandler
{
    /// <summary>The id of the operation row this handler runs.</summary>
    string OperationId { get; }

    Task<string> ExecuteAsync(Value a, Value b, CancellationToken cancellationToken);
}
