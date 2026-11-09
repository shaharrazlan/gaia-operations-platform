using Gaia.Core.Operations;

namespace Gaia.Core.Services;

/// <summary>
/// Run at startup: every operation without a formula must have a C# handler with the same id.
/// A missing one is a deployment mistake, so the app refuses to start instead of failing on first use.
/// </summary>
public static class HandlerCheck
{
    public static void EnsureEveryCodeOperationHasAHandler(IEnumerable<OperationDefinition> operations, IEnumerable<IOperationHandler> handlers)
    {
        var handled = handlers.Select(h => h.OperationId).ToHashSet();
        var missing = operations.Where(o => o.Formula is null && !handled.Contains(o.Id)).Select(o => o.Id).ToList();

        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"No handler is registered for code operation(s): {string.Join(", ", missing)}.");
    }
}
