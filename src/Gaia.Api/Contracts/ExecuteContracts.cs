using System.Text.Json.Serialization;
using Gaia.Core.Services;

namespace Gaia.Api.Contracts;

/// <summary>
/// Body of POST /api/operations/{id}/execute. Both fields are text (a JSON number is accepted too);
/// the operation decides how to read them.
/// </summary>
public sealed record ExecuteRequest(
    [property: JsonConverter(typeof(TextOrNumberConverter))] string? FieldA,
    [property: JsonConverter(typeof(TextOrNumberConverter))] string? FieldB);

/// <summary>
/// The result of running an operation, with its history: the 3 successful runs before this one (newest first)
/// and how many successful runs this operation had this month in Israel time, this one included (R8, R9).
/// History is null when the history store is unavailable; the result is still valid (R7.AC3).
/// </summary>
public sealed record ExecuteResponse(string OperationId, string? FieldA, string? FieldB, string Result, HistoryDto? History)
{
    public static ExecuteResponse From(ExecutionResult result) => new(
        result.OperationId,
        result.FieldA,
        result.FieldB,
        result.Result,
        result.History is { } history
            ? new HistoryDto([.. history.PreviousRuns.Select(r => new RunDto(r.FieldA, r.FieldB, r.Result, r.CreatedAtUtc))], history.MonthCount)
            : null);
}

public sealed record HistoryDto(IReadOnlyList<RunDto> PreviousRuns, int MonthCount);

/// <summary>One earlier successful run. <c>CreatedAtUtc</c> is UTC (ends in "Z").</summary>
public sealed record RunDto(string? FieldA, string? FieldB, string? Result, DateTime CreatedAtUtc);
