using System.Text.RegularExpressions;
using Gaia.Core.Errors;
using Gaia.Core.Formulas;
using Gaia.Core.Operations;

namespace Gaia.Core.Services;

/// <summary>
/// What an operator sends to add or change an operation (R4). Every field is optional here,
/// so the service, not the JSON layer, decides what is missing and says which field it is.
/// </summary>
public sealed record OperationInput(
    string? Id,
    string? Name,
    OperationKind? Kind,
    string? FieldALabel,
    string? FieldBLabel,
    InputType? InputType,
    string? Formula,
    bool IsEnabled = true,
    string? Version = null);

/// <summary>
/// The rules for managing the catalog without development (R4): validate a definition before it is saved,
/// so the catalog only ever holds operations that can run (R4.AC4).
/// </summary>
public sealed partial class CatalogService(IOperationRepository repository, TimeProvider time)
{
    public const int MaxNameLength = 100;
    public const int MaxLabelLength = 50;

    // Lower-case letters, digits and '-', starting with a letter, 2–50 characters: safe in a URL as-is.
    [GeneratedRegex("^[a-z][a-z0-9-]{1,49}$")]
    private static partial Regex IdPattern();

    /// <summary>Adds a custom formula operation. New operations are never system operations.</summary>
    public async Task<OperationDefinition> CreateAsync(OperationInput input, CancellationToken cancellationToken = default)
    {
        var id = input.Id ?? "";
        if (!IdPattern().IsMatch(id))
            throw new ValidationException(
                "Id must be 2–50 characters: lower-case letters, digits and '-', starting with a letter (e.g. 'average').", "id");

        var details = CheckDetails(input);
        var formula = CheckFormula(input.Formula);

        // Ids of deleted operations stay taken: their history (R7) still points at them.
        if (await repository.FindAsync(id, cancellationToken) is not null)
            throw new ConflictException($"An operation with id '{id}' already exists.", ErrorCodes.DuplicateId, "id");

        var now = time.GetUtcNow().UtcDateTime;
        return await repository.AddAsync(new OperationDefinition
        {
            Id = id,
            Name = details.Name,
            Kind = details.Kind,
            FieldALabel = details.FieldALabel,
            FieldBLabel = details.FieldBLabel,
            InputType = details.InputType,
            Formula = formula,
            IsSystem = false,
            IsEnabled = input.IsEnabled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        }, cancellationToken);
    }

    private sealed record Details(string Name, OperationKind Kind, string FieldALabel, string FieldBLabel, InputType InputType);

    /// <summary>Checks the fields every operation needs and returns them trimmed.</summary>
    private static Details CheckDetails(OperationInput input) => new(
        Required(input.Name, "name", "Name", MaxNameLength),
        Defined(input.Kind, "kind", "Kind"),
        Required(input.FieldALabel, "fieldALabel", "Field A label", MaxLabelLength),
        Required(input.FieldBLabel, "fieldBLabel", "Field B label", MaxLabelLength),
        Defined(input.InputType, "inputType", "Input type"));

    /// <summary>A custom operation always has a formula, and it must parse (R4.AC4).</summary>
    private static string CheckFormula(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
            throw new ValidationException("Formula is required, e.g. (A + B) / 2.", "formula", ErrorCodes.FormulaInvalid);
        var trimmed = formula.Trim();
        FormulaEngine.Validate(trimmed);   // throws formula_invalid with the position of the problem
        return trimmed;
    }

    private static string Required(string? value, string field, string label, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ValidationException($"{label} is required.", field);
        if (trimmed.Length > maxLength)
            throw new ValidationException($"{label} is longer than {maxLength} characters.", field);
        return trimmed;
    }

    private static T Defined<T>(T? value, string field, string label) where T : struct, Enum =>
        value is { } v && Enum.IsDefined(v)
            ? v
            : throw new ValidationException($"{label} is required: one of {string.Join(", ", Enum.GetNames<T>())}.", field);
}
