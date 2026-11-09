using Gaia.Core.Errors;
using Gaia.Core.Formulas;

namespace Gaia.Core.Tests.Formulas;

public class FunctionsTests
{
    private static Value Eval(string formula, Value a, Value b) =>
        Evaluator.Evaluate(FormulaParser.Parse(formula), a, b);

    private static Value Text(string formula, string a, string b) =>
        Eval(formula, new TextValue(a), new TextValue(b));

    private static Value Num(string formula, double a = 0, double b = 0) =>
        Eval(formula, new NumberValue(a), new NumberValue(b));

    [Fact]
    public void R3_AC2_concat_joins_texts()
    {
        Assert.Equal(new TextValue("foobar"), Text("concat(A, B)", "foo", "bar"));
    }

    [Fact]
    public void R3_AC2_concat_formats_numbers_like_results()
    {
        Assert.Equal(new TextValue("x=3.5"), Num("concat(\"x=\", A)", 3.5));
    }

    [Theory]
    [InlineData("banana", "an", 2)]
    [InlineData("aaaa", "aa", 2)]   // non-overlapping
    [InlineData("abc", "z", 0)]
    public void R3_AC2_count_counts_occurrences_of_B_in_A(string a, string b, double expected)
    {
        Assert.Equal(new NumberValue(expected), Text("count(A, B)", a, b));
    }

    [Theory]
    [InlineData("sqrt(A)", 16, 0, 4)]
    [InlineData("abs(A)", -3, 0, 3)]
    [InlineData("round(A)", 2.5, 0, 3)]
    [InlineData("round(A, 2)", 3.14159, 0, 3.14)]
    [InlineData("min(A, B)", 4, 9, 4)]
    [InlineData("max(A, B, 20)", 4, 9, 20)]
    [InlineData("floor(A)", 2.7, 0, 2)]
    [InlineData("ceil(A)", 2.1, 0, 3)]
    [InlineData("if(B == 0, 0, A / B)", 5, 0, 0)]  // the unused branch is never evaluated
    [InlineData("if(A > B, A, B)", 5, 9, 9)]
    public void T13_number_functions(string formula, double a, double b, double expected)
    {
        Assert.Equal(new NumberValue(expected), Num(formula, a, b));
    }

    [Theory]
    [InlineData("upper(A)", "Hello", "", "HELLO")]
    [InlineData("lower(A)", "Hello", "", "hello")]
    [InlineData("trim(A)", "  hi  ", "", "hi")]
    [InlineData("replace(A, B, \"-\")", "a b c", " ", "a-b-c")]
    public void T13_text_functions(string formula, string a, string b, string expected)
    {
        Assert.Equal(new TextValue(expected), Text(formula, a, b));
    }

    [Fact]
    public void T13_len_and_contains()
    {
        Assert.Equal(new NumberValue(5), Text("len(A)", "hello", ""));
        Assert.Equal(new NumberValue(2), Text("len(A)", "a\U0001F600", ""));   // emoji counts as 1
        Assert.Equal(new BoolValue(true), Text("contains(A, B)", "hello", "ell"));
    }

    [Theory]
    [InlineData("exec(A)", "Unknown function 'exec'")]
    [InlineData("Sqrt(A)", "Unknown function 'Sqrt'")]
    public void NFR6_unknown_function_is_rejected_at_parse_time(string formula, string message)
    {
        var error = Assert.Throws<ValidationException>(() => FormulaParser.Parse(formula));

        Assert.Equal(ErrorCodes.FormulaInvalid, error.Code);
        Assert.Contains(message, error.Message);
    }

    [Theory]
    [InlineData("sqrt(A, B)")]
    [InlineData("sqrt()")]
    [InlineData("if(A, B)")]
    [InlineData("replace(A, B)")]
    public void T13_wrong_arity_is_rejected_at_parse_time(string formula)
    {
        var error = Assert.Throws<ValidationException>(() => FormulaParser.Parse(formula));

        Assert.Contains("expects", error.Message);
    }

    [Fact]
    public void R2_AC5_sqrt_of_a_negative_number_is_a_domain_error()
    {
        Assert.Throws<DomainException>(() => Num("sqrt(A)", -1));
    }

    [Theory]
    [InlineData("upper(A)")]          // number where text is expected
    [InlineData("if(A, 1, 2)")]       // condition is not true/false
    [InlineData("round(A, 1.5)")]     // digits must be a whole number
    [InlineData("round(A, 16)")]      // digits out of range
    public void R2_AC5_wrong_argument_type_is_a_domain_error(string formula)
    {
        Assert.Throws<DomainException>(() => Num(formula, 1));
    }

    [Fact]
    public void NFR6_nested_replace_cannot_grow_text_without_bound()
    {
        var a = new string('x', 200);
        const string formula = "replace(replace(replace(A, \"x\", A), \"x\", A), \"x\", A)";

        var error = Assert.Throws<DomainException>(() => Text(formula, a, ""));

        Assert.Contains("longer than 1000", error.Message);
    }

    [Theory]
    [InlineData("count(A, B)")]
    [InlineData("replace(A, B, \"x\")")]
    public void R2_AC5_empty_search_text_is_a_domain_error(string formula)
    {
        var error = Assert.Throws<DomainException>(() => Text(formula, "abc", ""));

        Assert.Contains("empty", error.Message);
    }
}
