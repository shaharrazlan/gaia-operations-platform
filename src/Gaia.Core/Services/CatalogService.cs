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
    bool? IsEnabled = null,   // left out: enabled on create, unchanged on update
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
            IsEnabled = input.IsEnabled ?? true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        }, cancellationToken);
    }

    /// <summary>The management list: enabled and disabled operations (never deleted ones).</summary>
    public Task<IReadOnlyList<OperationDefinition>> ListAllAsync(CancellationToken cancellationToken = default) =>
        repository.ListAsync(includeDisabled: true, cancellationToken);

    /// <summary>
    /// Replaces an operation's definition, but only if nobody saved it since the operator loaded
    /// <see cref="OperationInput.Version"/> (R4.AC7). System operations keep their logic (R4.AC6).
    /// </summary>
    public async Task<OperationDefinition> UpdateAsync(string id, OperationInput input, CancellationToken cancellationToken = default)
    {
        var current = await FindExistingAsync(id, cancellationToken);
        if (input.Id is not null && input.Id != id)
            throw new ValidationException("Id can't be changed. Create a new operation instead.", "id");
        var version = DecodeVersion(input.Version);
        var details = CheckDetails(input);

        string? formula;
        if (current.IsSystem)
        {
            // Built-in logic is locked: only presentation (name, labels) and on/off may change.
            RejectLogicChange(details.Kind != current.Kind, "kind");
            RejectLogicChange(details.InputType != current.InputType, "inputType");
            RejectLogicChange(NullIfBlank(input.Formula) != current.Formula, "formula");
            formula = current.Formula;
        }
        else
        {
            formula = CheckFormula(input.Formula);
        }

        return await repository.UpdateAsync(new OperationDefinition
        {
            Id = current.Id,
            Name = details.Name,
            Kind = details.Kind,
            FieldALabel = details.FieldALabel,
            FieldBLabel = details.FieldBLabel,
            InputType = details.InputType,
            Formula = formula,
            IsSystem = current.IsSystem,
            IsEnabled = input.IsEnabled ?? current.IsEnabled,
            IsDeleted = false,
            CreatedAtUtc = current.CreatedAtUtc,
            UpdatedAtUtc = time.GetUtcNow().UtcDateTime,
        }, version, cancellationToken);
    }

    /// <summary>
    /// Removes a custom operation from the catalog. The row stays (soft delete) so its history is kept (R4.AC3).
    /// </summary>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var current = await FindExistingAsync(id, cancellationToken);
        if (current.IsSystem)
            throw new ValidationException(
                $"'{id}' is a built-in operation and can't be deleted. You can disable it instead.", code: ErrorCodes.SystemCannotDelete);

        var version = current.Version;
        current.IsDeleted = true;
        current.UpdatedAtUtc = time.GetUtcNow().UtcDateTime;
        await repository.UpdateAsync(current, version, cancellationToken);
    }

    private async Task<OperationDefinition> FindExistingAsync(string id, CancellationToken cancellationToken) =>
        await repository.FindAsync(id, cancellationToken) is { IsDeleted: false } operation
            ? operation
            : throw new NotFoundException($"Operation '{id}' was not found.");

    private static byte[] DecodeVersion(string? version)
    {
        const string message = "Version is required: send back the 'version' you received when you loaded the operation.";
        if (string.IsNullOrEmpty(version))
            throw new ValidationException(message, "version");
        try
        {
            return Convert.FromBase64String(version);
        }
        catch (FormatException)
        {
            throw new ValidationException(message, "version");
        }
    }

    private static void RejectLogicChange(bool changed, string field)
    {
        if (changed)
            throw new ValidationException(
                $"This is a built-in operation: its {field} can't be changed. You can rename or disable it.",
                field, ErrorCodes.SystemLogicReadonly);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
