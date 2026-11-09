using Gaia.Core.Operations;
using Gaia.Core.Services;

namespace Gaia.Api.Contracts;

/// <summary>
/// The body of POST (add) and PUT (change) on /api/operations. Every field is nullable on purpose:
/// the service checks what is missing and answers with the exact field name.
/// </summary>
public sealed class OperationUpsertDto
{
    /// <summary>Lower-case letters, digits and '-', starting with a letter, e.g. "average". On PUT it must match the URL (or be omitted).</summary>
    public string? Id { get; init; }

    /// <summary>The name users see in the operation list.</summary>
    public string? Name { get; init; }

    /// <summary>Arithmetic, String or Other.</summary>
    public OperationKind? Kind { get; init; }

    /// <summary>The label shown above Field A, e.g. "First number".</summary>
    public string? FieldALabel { get; init; }

    /// <summary>The label shown above Field B.</summary>
    public string? FieldBLabel { get; init; }

    /// <summary>How the two fields are read: Number, Text or Coordinate.</summary>
    public InputType? InputType { get; init; }

    /// <summary>A formula over A and B, e.g. "(A + B) / 2" or "concat(A, \" \", B)". Required for custom operations.</summary>
    public string? Formula { get; init; }

    /// <summary>Disabled operations are hidden from users but kept. Default: true.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>PUT only: the "version" you received when you loaded the operation (R4.AC7). Ignored on POST.</summary>
    public string? Version { get; init; }

    public OperationInput ToInput() =>
        new(Id, Name, Kind, FieldALabel, FieldBLabel, InputType, Formula, IsEnabled, Version);
}
