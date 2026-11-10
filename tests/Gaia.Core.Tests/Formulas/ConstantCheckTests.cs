using Gaia.Core.Errors;
using Gaia.Core.Formulas;
using Gaia.Core.Operations;

namespace Gaia.Core.Tests.Formulas;

/// <summary>R4.AC9: a fixed part that can never produce a result is refused when the formula is saved.</summary>
public class ConstantCheckTests
{
    [Theory]
    [InlineData("A / 0", InputType.Number, "'/' at position 3 always divides by 0, whatever is typed.")]
    [InlineData("A % (2 - 2)", InputType.Number, "'%' at position 3 always divides by 0, whatever is typed.")]
    [InlineData("sqrt(-1)", InputType.Number, "The part at position 1 can never produce a result, whatever is typed: The result is not a finite number.")]
    [InlineData("1 / 0 + A", InputType.Number, "The part at position 3 can never produce a result, whatever is typed: Division by zero.")]
    [InlineData("if(A > 0, 1 / 0, A)", InputType.Number, "Division by zero.")]
    [InlineData("round(A, 20)", InputType.Number, "round: the number of digits must be a whole number from 0 to 15.")]
    [InlineData("round(A, 1.5)", InputType.Number, "round: the number of digits must be a whole number")]
    [InlineData("count(A, \"\")", InputType.Text, "count: the text to count must not be empty.")]
    [InlineData("replace(A, \"\", \"x\")", InputType.Text, "replace: the text to find must not be empty.")]
    [InlineData("if(if(1 > 0, \"x\", 1 > 2), A, B)", InputType.Number, "if: the condition must be true/false, not a text.")]
    public void R4_AC9_a_fixed_part_that_can_never_produce_a_result_is_rejected(string formula, InputType inputType, string expected)
    {
        var error = Assert.Throws<ValidationException>(() => FormulaEngine.Validate(formula, inputType));

        Assert.Equal(ErrorCodes.FormulaInvalid, error.Code);
        Assert.Equal("formula", error.Field);
        Assert.Contains(expected, error.Message);
    }

    [Theory]
    [InlineData("A / B", InputType.Number)]                   // fails only when B is 0: a run-time error
    [InlineData("if(B == 0, 0, A / B)", InputType.Number)]
    [InlineData("A / (B - 1)", InputType.Number)]
    [InlineData("A / 0.5", InputType.Number)]
    [InlineData("2 ^ 10 + A", InputType.Number)]
    [InlineData("sqrt(16) * A", InputType.Number)]
    [InlineData("round(A, 2)", InputType.Number)]
    [InlineData("count(A, \"a\")", InputType.Text)]
    [InlineData("replace(A, \"a\", B)", InputType.Text)]
    [InlineData("if(1 > 2, 1 / 0, A)", InputType.Number)]    // the failing branch can never be chosen
    [InlineData("sqrt(A)", InputType.Number)]                 // fails only for negative A
    public void R4_AC9_formulas_that_can_produce_a_result_are_accepted(string formula, InputType inputType)
    {
        FormulaEngine.Validate(formula, inputType);
    }
}
