using Gaia.Core.Errors;

namespace Gaia.Core.Formulas;

/// <summary>Creates the "formula can't be run" error used by the tokenizer, parser and function checks.</summary>
internal static class FormulaErrors
{
    public static ValidationException Invalid(string message) =>
        new(message, "formula", ErrorCodes.FormulaInvalid);
}
