using Gaia.Core.Errors;
using Gaia.Core.Formulas;

namespace Gaia.Core.Tests.Formulas;

public class TokenizerTests
{
    [Fact]
    public void T9_tokenizes_numbers_strings_variables_operators_parentheses_and_commas_with_positions()
    {
        var tokens = Tokenizer.Tokenize("round(A * 2.5, 1) + \"x\" >= B");

        Assert.Equal(
            [
                (TokenKind.Identifier, "round", 1),
                (TokenKind.LeftParen, "(", 6),
                (TokenKind.Identifier, "A", 7),
                (TokenKind.Star, "*", 9),
                (TokenKind.Number, "2.5", 11),
                (TokenKind.Comma, ",", 14),
                (TokenKind.Number, "1", 16),
                (TokenKind.RightParen, ")", 17),
                (TokenKind.Plus, "+", 19),
                (TokenKind.Text, "x", 21),
                (TokenKind.GreaterEqual, ">=", 25),
                (TokenKind.Identifier, "B", 28),
                (TokenKind.End, "", 29),
            ],
            tokens.Select(t => (t.Kind, t.Text, t.Position)));
    }

    [Theory]
    [InlineData("-", TokenKind.Minus)]
    [InlineData("/", TokenKind.Slash)]
    [InlineData("%", TokenKind.Percent)]
    [InlineData("^", TokenKind.Caret)]
    [InlineData("==", TokenKind.EqualEqual)]
    [InlineData("!=", TokenKind.NotEqual)]
    [InlineData("<", TokenKind.Less)]
    [InlineData("<=", TokenKind.LessEqual)]
    [InlineData(">", TokenKind.Greater)]
    public void T9_tokenizes_every_operator(string source, TokenKind expected)
    {
        Assert.Equal(expected, Tokenizer.Tokenize(source)[0].Kind);
    }

    [Fact]
    public void R4_AC4_unexpected_character_reports_its_position()
    {
        var error = Assert.Throws<ValidationException>(() => Tokenizer.Tokenize("A + #B"));

        Assert.Equal(ErrorCodes.FormulaInvalid, error.Code);
        Assert.Equal("formula", error.Field);
        Assert.Contains("'#'", error.Message);
        Assert.Contains("position 5", error.Message);
    }

    [Fact]
    public void R4_AC4_unterminated_string_reports_its_position()
    {
        var error = Assert.Throws<ValidationException>(() => Tokenizer.Tokenize("concat(A, \"abc)"));

        Assert.Equal(ErrorCodes.FormulaInvalid, error.Code);
        Assert.Contains("Unterminated text", error.Message);
        Assert.Contains("position 11", error.Message);
    }

    [Fact]
    public void T9_a_doubled_quote_inside_text_is_one_quote()
    {
        var token = Tokenizer.Tokenize("\"say \"\"hi\"\"\"")[0];

        Assert.Equal(TokenKind.Text, token.Kind);
        Assert.Equal("say \"hi\"", token.Text);
    }

    [Fact]
    public void T9_single_equals_sign_is_rejected_with_a_hint()
    {
        var error = Assert.Throws<ValidationException>(() => Tokenizer.Tokenize("A = B"));

        Assert.Contains("'=='", error.Message);
    }
}
