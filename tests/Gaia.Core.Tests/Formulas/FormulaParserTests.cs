using System.Globalization;
using Gaia.Core.Errors;
using Gaia.Core.Formulas;

namespace Gaia.Core.Tests.Formulas;

public class FormulaParserTests
{
    /// <summary>Renders a tree fully parenthesised so tests can check how it was grouped.</summary>
    private static string Render(Node node) => node switch
    {
        NumberNode n => n.Value.ToString(CultureInfo.InvariantCulture),
        TextNode t => $"\"{t.Value}\"",
        VariableNode v => v.Name,
        UnaryNode u => $"(-{Render(u.Operand)})",
        BinaryNode b => $"({Render(b.Left)} {b.Operator} {Render(b.Right)})",
        CallNode c => $"{c.Name}({string.Join(", ", c.Arguments.Select(Render))})",
        _ => throw new InvalidOperationException(),
    };

    private static string Parse(string formula) => Render(FormulaParser.Parse(formula));

    [Theory]
    [InlineData("1 + 2 * 3 ^ 2", "(1 + (2 * (3 ^ 2)))")]
    [InlineData("(1 + 2) * 3", "((1 + 2) * 3)")]
    [InlineData("A - B - 1", "((A - B) - 1)")]
    [InlineData("A / B % 2", "((A / B) % 2)")]
    [InlineData("A + 1 >= B * 2", "((A + 1) >= (B * 2))")]
    public void T11_precedence_and_left_associativity(string formula, string expected)
    {
        Assert.Equal(expected, Parse(formula));
    }

    [Fact]
    public void T11_caret_is_right_associative()
    {
        Assert.Equal("(2 ^ (3 ^ 2))", Parse("2 ^ 3 ^ 2"));
    }

    [Theory]
    [InlineData("-A", "(-A)")]
    [InlineData("-2 ^ 2", "(-(2 ^ 2))")]
    [InlineData("2 ^ -1", "(2 ^ (-1))")]
    [InlineData("A * -B", "(A * (-B))")]
    public void T11_unary_minus(string formula, string expected)
    {
        Assert.Equal(expected, Parse(formula));
    }

    [Fact]
    public void T11_function_calls_and_text_literals()
    {
        Assert.Equal("concat(\"Hi \", A, B)", Parse("concat(\"Hi \", A, B)"));
        Assert.Equal("round(sqrt(A), 2)", Parse("round(sqrt(A), 2)"));
    }

    [Theory]
    [InlineData("(A + B", "Expected ')'", 7)]
    [InlineData("A +", "Expected a value", 4)]
    [InlineData("A B", "Unexpected 'B'", 3)]
    [InlineData("max(A, )", "Expected a value", 8)]
    public void R4_AC4_syntax_errors_report_their_position(string formula, string message, int position)
    {
        var error = Assert.Throws<ValidationException>(() => FormulaParser.Parse(formula));

        Assert.Equal(ErrorCodes.FormulaInvalid, error.Code);
        Assert.Contains(message, error.Message);
        Assert.Contains($"position {position}", error.Message);
    }

    [Theory]
    [InlineData("C")]
    [InlineData("a + 1")]
    [InlineData("Math + 1")]
    public void R4_AC4_unknown_identifier_is_an_error(string formula)
    {
        var error = Assert.Throws<ValidationException>(() => FormulaParser.Parse(formula));

        Assert.Contains("Unknown name", error.Message);
    }

    [Fact]
    public void R4_AC4_number_literal_too_large_for_a_double_is_an_error()
    {
        var error = Assert.Throws<ValidationException>(() => FormulaParser.Parse("1" + new string('0', 400)));

        Assert.Contains("too large", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void R4_AC4_empty_formula_is_an_error(string formula)
    {
        Assert.Throws<ValidationException>(() => FormulaParser.Parse(formula));
    }

    [Theory]
    [InlineData("File.Delete(\"x\")")]
    [InlineData("System.IO.File.ReadAllText(\"c:/x\")")]
    [InlineData("A.GetType()")]
    [InlineData("A; B")]
    public void NFR6_code_like_input_is_a_parse_error(string formula)
    {
        var error = Assert.Throws<ValidationException>(() => FormulaParser.Parse(formula));

        Assert.Equal(ErrorCodes.FormulaInvalid, error.Code);
    }

    [Fact]
    public void NFR6_nesting_deeper_than_50_is_rejected()
    {
        var deep = new string('(', 51) + "A" + new string(')', 51);

        var error = Assert.Throws<ValidationException>(() => FormulaParser.Parse(deep));

        Assert.Contains("nested", error.Message);
    }

    [Fact]
    public void NFR6_long_chain_of_unary_minus_is_rejected_not_a_stack_overflow()
    {
        var error = Assert.Throws<ValidationException>(() => FormulaParser.Parse(new string('-', 400) + "A"));

        Assert.Contains("nested", error.Message);
    }

    [Fact]
    public void NFR6_nesting_up_to_the_limit_is_allowed()
    {
        // The whole formula is level 1, so 49 parentheses reach level 50.
        var ok = new string('(', 49) + "A" + new string(')', 49);

        Assert.Equal("A", Parse(ok));
    }

    [Fact]
    public void NFR6_formula_longer_than_500_characters_is_rejected()
    {
        var tooLong = string.Join(" + ", Enumerable.Repeat("A", 200)); // 797 characters

        var error = Assert.Throws<ValidationException>(() => FormulaParser.Parse(tooLong));

        Assert.Contains("500", error.Message);
    }
}
