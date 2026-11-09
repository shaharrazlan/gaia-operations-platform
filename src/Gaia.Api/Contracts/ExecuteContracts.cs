namespace Gaia.Api.Contracts;

/// <summary>Body of POST /api/operations/{id}/execute. Both fields are text; the operation decides how to read them.</summary>
public sealed record ExecuteRequest(string? FieldA, string? FieldB);

/// <summary>The result of running an operation.</summary>
public sealed record ExecuteResponse(string OperationId, string? FieldA, string? FieldB, string Result);
