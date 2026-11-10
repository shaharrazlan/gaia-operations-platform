using Gaia.Core.History;
using Microsoft.Extensions.Logging;

namespace Gaia.Core.Tests.History;

/// <summary>In-memory run store for unit tests. Set <see cref="Broken"/> to simulate the database being down.</summary>
internal sealed class FakeRunRepository : IRunRepository
{
    private long _nextId = 1;
    public List<OperationRun> Runs { get; } = [];
    public bool Broken { get; set; }

    public Task AddAsync(OperationRun run, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();   // like the real database call
        ThrowIfBroken();
        run.Id = _nextId++;
        Runs.Add(run);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OperationRun>> LatestSuccessfulAsync(string operationId, long exceptRunId, int count,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfBroken();
        return Task.FromResult<IReadOnlyList<OperationRun>>(Runs
            .Where(r => r.OperationId == operationId && r.IsSuccess && r.Id != exceptRunId)
            .OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id)
            .Take(count).ToList());
    }

    public Task<int> CountSuccessfulSinceAsync(string operationId, DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfBroken();
        return Task.FromResult(Runs.Count(r => r.OperationId == operationId && r.IsSuccess && r.CreatedAtUtc >= sinceUtc));
    }

    private void ThrowIfBroken()
    {
        if (Broken)
            throw new InvalidOperationException("history store is down");
    }
}

/// <summary>Keeps every log entry so tests can check that a failure was logged.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception), exception));
}
