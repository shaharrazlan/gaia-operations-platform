namespace Gaia.Core.History;

/// <summary>Stores and reads operation runs (R7–R9). Implemented with EF Core in Gaia.Infrastructure.</summary>
public interface IRunRepository
{
    /// <summary>Stores the run; afterwards <see cref="OperationRun.Id"/> holds its database id.</summary>
    Task AddAsync(OperationRun run, CancellationToken cancellationToken = default);

    /// <summary>The newest successful runs of an operation, newest first, leaving out the run <paramref name="exceptRunId"/>.</summary>
    Task<IReadOnlyList<OperationRun>> LatestSuccessfulAsync(string operationId, long exceptRunId, int count,
        CancellationToken cancellationToken = default);

    /// <summary>How many successful runs of an operation started at or after <paramref name="sinceUtc"/>.</summary>
    Task<int> CountSuccessfulSinceAsync(string operationId, DateTime sinceUtc, CancellationToken cancellationToken = default);
}
