namespace Gaia.Core.Operations;

/// <summary>Reads (and, from S6, writes) the operation catalog. Implemented with EF Core in Gaia.Infrastructure.</summary>
public interface IOperationRepository
{
    /// <summary>Operations ordered by name. Deleted ones are never returned; disabled ones only on request.</summary>
    Task<IReadOnlyList<OperationDefinition>> ListAsync(bool includeDisabled, CancellationToken cancellationToken = default);

    /// <summary>The operation with this id in any state (also disabled or deleted), or null.</summary>
    Task<OperationDefinition?> FindAsync(string id, CancellationToken cancellationToken = default);
}
