using Gaia.Core.Errors;
using Gaia.Core.Formulas;

namespace Gaia.Core.Tests.Formulas;

public class EvaluatorTests
{
    private static Value Eval(string formula, Value a, Value b) =>
        Evaluator.Evaluate(FormulaParser.Parse(formula), a, b);

    private static Value Eval(string formula, double a, double b) =>
        Eval(formula, new NumberValue(a), new NumberValue(b));

    [Theory]
    [InlineData("A + B", 9)]
    [InlineData("A - B", 5)]
    [InlineData("A * B", 14)]
    [InlineData("A / B", 3.5)]
    [InlineData("A % B", 1)]
    [InlineData("A ^ B", 49)]
    [InlineData("-A", -7)]
    [InlineData("2 ^ -1", 0.5)]
    [InlineData("(A + B) * 2 - 1", 17)]
    public void T12_arithmetic_operators(string formula, double expected)
    {
        Assert.Equal(new NumberValue(expected), Eval(formula, 7, 2));
    }

    [Theory]
    [InlineData("A > B", true)]
    [InlineData("A >= 7", true)]
    [InlineData("A < B", false)]
    [InlineData("A <= 2", false)]
    [InlineData("A == 7", true)]
    [InlineData("A != B", true)]
    [InlineData("(A > B) == (B > A)", false)]
    public void T12_comparison_operators(string formula, bool expected)
    {
        Assert.Equal(new BoolValue(expected), Eval(formula, 7, 2));
    }

    [Fact]
    public void T12_plus_concatenates_two_texts()
    {
        Assert.Equal(new TextValue("foobar"), Eval("A + B", new TextValue("foo"), new TextValue("bar")));
    }

    [Fact]
    public void T12_texts_compare_by_ordinal_value()
    {
        var foo = new TextValue("foo");
        var bar = new TextValue("bar");

        Assert.Equal(new BoolValue(true), Eval("A == \"foo\"", foo, bar));
        Assert.Equal(new BoolValue(true), Eval("B < A", foo, bar));
    }

    [Fact]
    public void R2_AC5_division_by_zero_is_a_domain_error()
    {
        var error = Assert.Throws<DomainException>(() => Eval("A / B", 5, 0));

        Assert.Equal(ErrorCodes.Domain, error.Code);
        Assert.Contains("Division by zero", error.Message);
    }

    [Fact]
    public void R2_AC5_modulo_by_zero_is_a_domain_error()
    {
        var error = Assert.Throws<DomainException>(() => Eval("A % B", 5, 0));

        Assert.Contains("Modulo by zero", error.Message);
    }

    [Theory]
    [InlineData("(0 - A) ^ 0.5")]   // square root of a negative number → NaN
    [InlineData("A ^ 400")]         // overflows to infinity
    public void R2_AC5_NaN_or_infinite_result_is_a_domain_error(string formula)
    {
        var error = Assert.Throws<DomainException>(() => Eval(formula, 10, 1));

        Assert.Contains("finite", error.Message);
    }

    [Theory]
    [InlineData("A + B")]
    [InlineData("A * 2")]
    [InlineData("-A")]
    [InlineData("A > 1")]
    public void R2_AC5_mixing_number_and_text_is_a_domain_error(string formula)
    {
        Assert.Throws<DomainException>(() => Eval(formula, new TextValue("x"), new NumberValue(1)));
    }
}
