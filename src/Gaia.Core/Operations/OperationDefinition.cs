namespace Gaia.Core.Operations;

/// <summary>
/// One operation in the catalog, stored as a row in the Operations table.
/// Two independent axes (plan, "Operation model"):
/// <list type="bullet">
/// <item><b>Owner</b> — <see cref="IsSystem"/>: shipped with the platform (logic locked) or created by an operator.</item>
/// <item><b>Implementation</b> — <see cref="Formula"/>: a formula, or null for a C# code handler (e.g. weather).</item>
/// </list>
/// </summary>
public sealed class OperationDefinition
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public OperationKind Kind { get; set; }
    public required string FieldALabel { get; set; }
    public required string FieldBLabel { get; set; }
    public InputType InputType { get; set; }

    /// <summary>The formula over A and B, or null when a code handler with the same id runs the operation.</summary>
    public string? Formula { get; set; }

    public bool IsSystem { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>Soft delete: the row and its history stay, the operation disappears everywhere else (R4.AC3).</summary>
    public bool IsDeleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Changed by SQL Server on every save (rowversion). A save based on an older version is rejected,
    /// so two operators can't silently overwrite each other (R4.AC7).
    /// </summary>
    public byte[] Version { get; set; } = [];

    /// <summary>Usable by end users: enabled and not deleted.</summary>
    public bool IsAvailable => IsEnabled && !IsDeleted;
}
