namespace Gaia.Core.History;

/// <summary>
/// One execution of an operation and its outcome, stored in the OperationRuns table (R7.AC1).
/// Failed runs are stored too (with their error), but the history shown to users counts successful runs only (A9).
/// </summary>
public sealed class OperationRun
{
    public const int MaxFieldLength = 200;
    public const int MaxResultLength = 1000;
    public const int MaxErrorMessageLength = 500;

    /// <summary>Set by the database when the run is stored.</summary>
    public long Id { get; set; }

    public required string OperationId { get; init; }
    public string? FieldA { get; init; }
    public string? FieldB { get; init; }

    /// <summary>The result text of a successful run; null for a failed one.</summary>
    public string? Result { get; init; }

    public bool IsSuccess { get; init; }

    /// <summary>For a failed run: the error code (e.g. "domain_error") and the message the caller got.</summary>
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public int DurationMs { get; init; }
}
