using Gaia.Core.Operations;

namespace Gaia.Core.Services;

/// <summary>
/// Run at startup: every live operation without a formula must have exactly one C# handler with the same id.
/// A missing or duplicate handler is a deployment mistake, so the app refuses to start instead of failing on first use.
/// </summary>
public static class HandlerCheck
{
    public static void EnsureEveryCodeOperationHasAHandler(IEnumerable<OperationDefinition> operations, IEnumerable<IOperationHandler> handlers)
    {
        var handlerIds = handlers.Select(h => h.OperationId).ToList();

        var duplicates = handlerIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                $"More than one handler is registered for operation(s): {string.Join(", ", duplicates)}.");

        // Deleted operations can't be run, so a retired handler may be removed from the code.
        var missing = operations
            .Where(o => o.Formula is null && !o.IsDeleted && !handlerIds.Contains(o.Id))
            .Select(o => o.Id)
            .ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"No handler is registered for code operation(s): {string.Join(", ", missing)}.");
    }
}
