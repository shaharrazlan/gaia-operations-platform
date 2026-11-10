namespace Gaia.Core.Tests;

/// <summary>A clock that always says the same time, so tests don't depend on when they run.</summary>
internal sealed class FixedTime(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow);
}
