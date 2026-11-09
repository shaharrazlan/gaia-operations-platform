using System.Text.Json.Serialization;

namespace Gaia.Api.Contracts;

/// <summary>
/// Body of POST /api/operations/{id}/execute. Both fields are text (a JSON number is accepted too);
/// the operation decides how to read them.
/// </summary>
public sealed record ExecuteRequest(
    [property: JsonConverter(typeof(TextOrNumberConverter))] string? FieldA,
    [property: JsonConverter(typeof(TextOrNumberConverter))] string? FieldB);

/// <summary>The result of running an operation.</summary>
public sealed record ExecuteResponse(string OperationId, string? FieldA, string? FieldB, string Result);
