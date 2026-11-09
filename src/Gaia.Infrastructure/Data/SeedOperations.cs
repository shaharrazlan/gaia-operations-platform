using Gaia.Core.Operations;

namespace Gaia.Infrastructure.Data;

/// <summary>
/// The operations the platform ships with (inserted by the Initial migration).
/// System operations: logic locked (rename/disable only). Custom ones: examples an operator may edit or delete.
/// Only "weather" has no formula — it is run by a C# code handler.
/// </summary>
public static class SeedOperations
{
    // Fixed so the migration is deterministic (HasData needs constant values).
    private static readonly DateTime Seeded = new(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Builds new objects on every call, so no caller can change the seed seen by others.</summary>
    public static OperationDefinition[] Create() =>
    [
        System("add", "Add", OperationKind.Arithmetic, "First number", "Second number", InputType.Number, "A + B"),
        System("subtract", "Subtract", OperationKind.Arithmetic, "First number", "Second number", InputType.Number, "A - B"),
        System("multiply", "Multiply", OperationKind.Arithmetic, "First number", "Second number", InputType.Number, "A * B"),
        System("divide", "Divide", OperationKind.Arithmetic, "Dividend", "Divisor", InputType.Number, "A / B"),
        System("power", "Power", OperationKind.Arithmetic, "Base", "Exponent", InputType.Number, "A ^ B"),
        System("concat", "Concatenate", OperationKind.String, "First text", "Second text", InputType.Text, "concat(A, B)"),
        System("count", "Count occurrences", OperationKind.String, "Text", "Text to count", InputType.Text, "count(A, B)"),
        System("weather", "Weather today", OperationKind.Other, "Latitude", "Longitude", InputType.Coordinate, formula: null),
        Custom("average", "Average", OperationKind.Arithmetic, "First number", "Second number", InputType.Number, "(A + B) / 2"),
        Custom("hypotenuse", "Hypotenuse", OperationKind.Arithmetic, "Side a", "Side b", InputType.Number, "sqrt(A * A + B * B)"),
    ];

    private static OperationDefinition System(string id, string name, OperationKind kind, string labelA, string labelB, InputType input, string? formula) =>
        Create(id, name, kind, labelA, labelB, input, formula, isSystem: true);

    private static OperationDefinition Custom(string id, string name, OperationKind kind, string labelA, string labelB, InputType input, string formula) =>
        Create(id, name, kind, labelA, labelB, input, formula, isSystem: false);

    private static OperationDefinition Create(string id, string name, OperationKind kind, string labelA, string labelB, InputType input, string? formula, bool isSystem) => new()
    {
        Id = id,
        Name = name,
        Kind = kind,
        FieldALabel = labelA,
        FieldBLabel = labelB,
        InputType = input,
        Formula = formula,
        IsSystem = isSystem,
        IsEnabled = true,
        CreatedAtUtc = Seeded,
        UpdatedAtUtc = Seeded,
    };
}
