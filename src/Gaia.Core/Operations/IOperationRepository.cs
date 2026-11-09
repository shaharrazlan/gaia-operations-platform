namespace Gaia.Core.Operations;

/// <summary>Reads and writes the operation catalog. Implemented with EF Core in Gaia.Infrastructure.</summary>
public interface IOperationRepository
{
    /// <summary>Operations ordered by name. Deleted ones are never returned; disabled ones only on request.</summary>
    Task<IReadOnlyList<OperationDefinition>> ListAsync(bool includeDisabled, CancellationToken cancellationToken = default);

    /// <summary>The operation with this id in any state (also disabled or deleted), or null.</summary>
    Task<OperationDefinition?> FindAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new operation and returns it with its first <see cref="OperationDefinition.Version"/>.
    /// Throws a ConflictException (duplicate_id) if the id was taken meanwhile.
    /// </summary>
    Task<OperationDefinition> AddAsync(OperationDefinition operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a changed operation only if the stored version is still <paramref name="expectedVersion"/>,
    /// and returns it with its new version. Otherwise throws a ConflictException (edit_conflict) (R4.AC7).
    /// </summary>
    Task<OperationDefinition> UpdateAsync(OperationDefinition operation, byte[] expectedVersion,
        CancellationToken cancellationToken = default);
}
