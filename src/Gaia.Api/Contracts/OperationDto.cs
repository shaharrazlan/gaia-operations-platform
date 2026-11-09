using Gaia.Core.Operations;

namespace Gaia.Api.Contracts;

/// <summary>An operation as the API returns it. Enums are serialized as their names ("Arithmetic").</summary>
public sealed record OperationDto(
    string Id,
    string Name,
    OperationKind Kind,
    string FieldALabel,
    string FieldBLabel,
    InputType InputType,
    string? Formula,
    bool IsSystem,
    bool IsEnabled,
    string Version)
{
    public static OperationDto From(OperationDefinition operation) => new(
        operation.Id,
        operation.Name,
        operation.Kind,
        operation.FieldALabel,
        operation.FieldBLabel,
        operation.InputType,
        operation.Formula,
        operation.IsSystem,
        operation.IsEnabled,
        Convert.ToBase64String(operation.Version));
}
