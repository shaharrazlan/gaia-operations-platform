using Gaia.Core.Operations;

namespace Gaia.Core.Formulas;

/// <summary>
/// The one entry point the rest of the platform uses for formulas:
/// <see cref="Validate"/> when an operator saves a formula, <see cref="Evaluate"/> when a user runs it.
/// Pipeline: text inputs → <see cref="InputParser"/> → <see cref="Tokenizer"/> → <see cref="FormulaParser"/>
/// → <see cref="Evaluator"/> → <see cref="ResultFormatter"/>.
/// </summary>
public static class FormulaEngine
{
    /// <summary>
    /// Throws a ValidationException (code formula_invalid, with position) if the formula can't be run:
    /// it doesn't parse (R4.AC4), its parts can't work with the input type (R4.AC8), or a fixed part can never
    /// produce a result (R4.AC9).
    /// </summary>
    public static void Validate(string formula, InputType inputType)
    {
        var tree = FormulaParser.Parse(formula);
        TypeChecker.Check(tree, inputType);
        ConstantCheck.Check(tree);
    }

    /// <summary>Runs the formula on the two raw inputs and returns the formatted result.</summary>
    public static string Evaluate(string formula, InputType inputType, string? fieldA, string? fieldB)
    {
        var (a, b) = InputParser.Parse(inputType, fieldA, fieldB);
        var tree = FormulaParser.Parse(formula);
        return ResultFormatter.Format(Evaluator.Evaluate(tree, a, b));
    }
}
