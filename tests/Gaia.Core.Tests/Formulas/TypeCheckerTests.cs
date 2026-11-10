using Gaia.Core.Errors;
using Gaia.Core.Formulas;
using Gaia.Core.Operations;

namespace Gaia.Core.Tests.Formulas;

/// <summary>R4.AC8: a formula whose parts can't work with the input type is refused when it is saved.</summary>
public class TypeCheckerTests
{
    private const string TextHint = "A and B are text because the input type is Text";
    private const string NumberHint = "A and B are numbers because the input type is Number";

    public static TheoryData<string, InputType, string> Rejected => new()
    {
        // operators
        { "A ^ B", InputType.Text, "'^' at position 3 needs two numbers, but here it gets a text and a text." },
        { "A ^ B", InputType.Text, TextHint },
        { "A * 3 + B", InputType.Text, "'*' at position 3 needs two numbers" },
        { "-A", InputType.Text, "'-' at position 1 needs a number, but here it gets a text." },
        { "A + \"kg\"", InputType.Number, "'+' at position 3 needs two numbers or two texts, but here it gets a number and a text." },
        { "A + \"kg\"", InputType.Number, NumberHint },
        { "A == \"5\"", InputType.Number, "'==' at position 3 needs two values of the same type" },
        { "A < \"z\"", InputType.Number, "'<' at position 3 needs two numbers or two texts" },
        { "(A > B) * 2", InputType.Number, "'*' at position 9 needs two numbers, but here it gets a true/false value and a number." },
        { "A * B", InputType.Text, TextHint },
        // functions
        { "upper(A)", InputType.Number, "upper at position 1 needs a text as argument 1, but here it gets a number." },
        { "upper(A)", InputType.Number, NumberHint },
        { "upper(A)", InputType.Coordinate, "A and B are numbers because the input type is Coordinate" },
        { "sqrt(A)", InputType.Text, "sqrt at position 1 needs a number as argument 1, but here it gets a text." },
        { "len(A)", InputType.Number, "len at position 1 needs a text as argument 1" },
        { "count(A, 2)", InputType.Text, "count at position 1 needs a text as argument 2, but here it gets a number." },
        { "round(A, \"2\")", InputType.Number, "round at position 1 needs a number as argument 2" },
        { "max(A, B, \"x\")", InputType.Number, "max at position 1 needs a number as argument 3" },
        { "upper(A) * 2", InputType.Text, "'*' at position 10 needs two numbers, but here it gets a text and a number." },
        // if
        { "if(A, 1, 2)", InputType.Number, "if at position 1 needs a true/false condition (e.g. A > B) as argument 1, but here it gets a number." },
        { "if(contains(A, B), len(A), 0) + upper(B)", InputType.Text, "'+' at position 31 needs two numbers or two texts, but here it gets a number and a text." },
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public void R4_AC8_a_formula_whose_types_cannot_work_is_rejected_with_a_plain_message(string formula, InputType inputType, string expected)
    {
        var error = Assert.Throws<ValidationException>(() => FormulaEngine.Validate(formula, inputType));

        Assert.Equal(ErrorCodes.FormulaInvalid, error.Code);
        Assert.Equal("formula", error.Field);
        Assert.Contains(expected, error.Message);
    }

    [Theory]
    [InlineData("(A > B) * 2", InputType.Number)]   // no A or B at the failing operator itself
    [InlineData("count(A, 2)", InputType.Text)]     // the wrong argument is the literal 2, not A or B
    [InlineData("A * \"x\"", InputType.Number)]     // A is a number, as '*' needs; the text "x" is at fault
    public void R4_AC8_the_input_type_hint_appears_only_when_A_or_B_is_at_fault(string formula, InputType inputType)
    {
        var error = Assert.Throws<ValidationException>(() => FormulaEngine.Validate(formula, inputType));

        Assert.DoesNotContain("because the input type is", error.Message);
    }

    [Theory]
    [InlineData("A ^ B", InputType.Number)]
    [InlineData("A * B", InputType.Coordinate)]
    [InlineData("concat(\"Total: \", A * 2)", InputType.Number)]   // number → text
    [InlineData("len(A) + len(B)", InputType.Text)]                 // text → number
    [InlineData("count(A, B) * 10", InputType.Text)]
    [InlineData("upper(A) + \" \" + B", InputType.Text)]
    [InlineData("if(A > B, A, \"none\")", InputType.Number)]        // branches of different types (A11)
    [InlineData("if(A > B, A, \"none\") + 1", InputType.Number)]    // unknown type: left to run time (A11)
    [InlineData("A > B", InputType.Number)]                         // true/false as the result (A11)
    [InlineData("A * 2", InputType.Number)]                         // ignores B (A11)
    [InlineData("if(contains(A, B), 1, 0) + 1", InputType.Text)]
    [InlineData("concat(A > B, \"!\")", InputType.Number)]
    [InlineData("max(A, B, 3)", InputType.Number)]
    [InlineData("A == B", InputType.Text)]
    [InlineData("\"x\" + \"y\"", InputType.Number)]
    public void R4_AC8_formulas_whose_types_work_are_accepted(string formula, InputType inputType)
    {
        FormulaEngine.Validate(formula, inputType);
    }

    [Fact]
    public void R4_AC8_A_power_B_with_text_inputs_still_fails_at_run_time_with_the_same_plain_wording()
    {
        // An operation saved before this check existed can still hold such a formula.
        var error = Assert.Throws<DomainException>(() => FormulaEngine.Evaluate("A ^ B", InputType.Text, "5", "6"));

        Assert.Equal("'^' at position 3 needs two numbers, but here it gets a text and a text.", error.Message);
    }
}
