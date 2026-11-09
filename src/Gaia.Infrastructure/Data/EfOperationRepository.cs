using Gaia.Core.Operations;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Infrastructure.Data;

public class EfOperationRepository(GaiaDbContext db) : IOperationRepository
{
    public async Task<IReadOnlyList<OperationDefinition>> ListAsync(bool includeDisabled, CancellationToken cancellationToken = default) =>
        await db.Operations
            .AsNoTracking()
            .Where(o => !o.IsDeleted && (includeDisabled || o.IsEnabled))
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

    public Task<OperationDefinition?> FindAsync(string id, CancellationToken cancellationToken = default) =>
        db.Operations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
}
