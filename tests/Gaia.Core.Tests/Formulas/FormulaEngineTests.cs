using Gaia.Core.Errors;
using Gaia.Core.Formulas;
using Gaia.Core.Operations;

namespace Gaia.Core.Tests.Formulas;

public class FormulaEngineTests
{
    [Theory]
    [InlineData("A + B", "9")]
    [InlineData("A - B", "5")]
    [InlineData("A * B", "14")]
    [InlineData("A / B", "3.5")]
    [InlineData("A ^ B", "49")]
    public void R3_AC1_seeded_arithmetic_formulas_with_A_7_and_B_2(string formula, string expected)
    {
        Assert.Equal(expected, FormulaEngine.Evaluate(formula, InputType.Number, "7", "2"));
    }

    [Fact]
    public void R3_AC2_seeded_text_formulas()
    {
        Assert.Equal("foobar", FormulaEngine.Evaluate("concat(A, B)", InputType.Text, "foo", "bar"));
        Assert.Equal("2", FormulaEngine.Evaluate("count(A, B)", InputType.Text, "banana", "an"));
    }

    [Theory]
    [InlineData("A + B", "0.1", "0.2", "0.3")]
    [InlineData("-A", "0", "1", "0")]               // never "-0"
    [InlineData("A / B", "1", "3", "0.333333333333333")]
    [InlineData("A > B", "2", "1", "true")]
    public void T14_results_are_formatted_cleanly(string formula, string a, string b, string expected)
    {
        Assert.Equal(expected, FormulaEngine.Evaluate(formula, InputType.Number, a, b));
    }

    [Fact]
    public void T14_Validate_returns_error_with_position_for_a_bad_formula()
    {
        var error = Assert.Throws<ValidationException>(() => FormulaEngine.Validate("A + * B"));

        Assert.Equal(ErrorCodes.FormulaInvalid, error.Code);
        Assert.Contains("position 5", error.Message);
    }

    [Fact]
    public void T14_Validate_accepts_a_good_formula()
    {
        FormulaEngine.Validate("sqrt(A*A + B*B)");
    }

    [Fact]
    public void T14_Evaluate_validates_inputs_by_input_type()
    {
        var error = Assert.Throws<ValidationException>(() => FormulaEngine.Evaluate("A + B", InputType.Number, "x", "1"));

        Assert.Equal("fieldA", error.Field);
    }
}
