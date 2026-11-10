using Gaia.Core.History;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Infrastructure.Data;

public class EfRunRepository(GaiaDbContext db) : IRunRepository
{
    public async Task AddAsync(OperationRun run, CancellationToken cancellationToken = default)
    {
        db.Runs.Add(run);
        try
        {
            await db.SaveChangesAsync(cancellationToken);   // fills in run.Id
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task<IReadOnlyList<OperationRun>> LatestSuccessfulAsync(string operationId, long exceptRunId, int count,
        CancellationToken cancellationToken = default) =>
        await db.Runs
            .AsNoTracking()
            .Where(r => r.OperationId == operationId && r.IsSuccess && r.Id != exceptRunId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.Id)   // runs in the same instant: the later insert is the newer one
            .Take(count)
            .ToListAsync(cancellationToken);

    public Task<int> CountSuccessfulSinceAsync(string operationId, DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        db.Runs.CountAsync(r => r.OperationId == operationId && r.IsSuccess && r.CreatedAtUtc >= sinceUtc, cancellationToken);
}
