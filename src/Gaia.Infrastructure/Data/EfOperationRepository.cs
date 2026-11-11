using Gaia.Core.Errors;
using Gaia.Core.Operations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Infrastructure.Data;

public class EfOperationRepository(GaiaDbContext db) : IOperationRepository
{
    // SQL Server error numbers for "duplicate key": primary key / unique constraint, unique index.
    private const int DuplicateKey = 2627;
    private const int DuplicateIndexKey = 2601;

    public async Task<IReadOnlyList<OperationDefinition>> ListAsync(bool includeDisabled, CancellationToken cancellationToken = default) =>
        await db.Operations
            .AsNoTracking()
            .Where(o => !o.IsDeleted && (includeDisabled || o.IsEnabled))
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

    public Task<OperationDefinition?> FindAsync(string id, CancellationToken cancellationToken = default) =>
        db.Operations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<OperationDefinition> AddAsync(OperationDefinition operation, CancellationToken cancellationToken = default)
    {
        db.Operations.Add(operation);
        try
        {
            await db.SaveChangesAsync(cancellationToken);   // fills in operation.Version
        }
        catch (DbUpdateException e) when (IsDuplicate(e, out var nameClash))
        {
            // Another request saved the same id or name between the service's check and this insert.
            throw nameClash
                ? DuplicateName(operation)
                : new ConflictException($"An operation with id '{operation.Id}' already exists.", ErrorCodes.DuplicateId, "id");
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
        return operation;
    }

    public async Task<OperationDefinition> UpdateAsync(OperationDefinition operation, byte[] expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var entry = db.Operations.Update(operation);
        // The UPDATE's WHERE clause compares Version with this value; if someone saved in between, no row matches.
        entry.Property(o => o.Version).OriginalValue = expectedVersion;
        try
        {
            await db.SaveChangesAsync(cancellationToken);   // fills in the new operation.Version
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "This operation was changed by someone else. Reload to see the latest version.", ErrorCodes.EditConflict);
        }
        catch (DbUpdateException e) when (IsDuplicate(e, out var nameClash) && nameClash)
        {
            // Renamed onto a name another request took between the service's check and this save.
            throw DuplicateName(operation);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
        return operation;
    }

    /// <summary>Is this SQL Server's "duplicate key" error? <paramref name="nameClash"/>: was it the unique-name index (R4.AC10)?</summary>
    private static bool IsDuplicate(DbUpdateException e, out bool nameClash)
    {
        var sql = e.InnerException as SqlException;
        nameClash = sql?.Message.Contains(OperationEntityConfig.NameIndex, StringComparison.Ordinal) ?? false;
        return sql is { Number: DuplicateKey or DuplicateIndexKey };
    }

    private static ConflictException DuplicateName(OperationDefinition operation) =>
        new($"An operation named '{operation.Name}' already exists. Choose another name.", ErrorCodes.DuplicateName, "name");
}
