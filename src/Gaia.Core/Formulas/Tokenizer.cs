using System.Text;

namespace Gaia.Core.Formulas;

/// <summary>
/// Step 1 of 3 (tokenize → parse → evaluate): splits a formula such as <c>round(A * 2.5, 1)</c>
/// into tokens. Only the characters of the formula language are accepted; anything else
/// (<c>.</c>, <c>#</c>, <c>;</c>, …) is an error, which is the first layer of the "no arbitrary code" guarantee.
/// </summary>
public static class Tokenizer
{
    public static IReadOnlyList<Token> Tokenize(string formula)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < formula.Length)
        {
            var c = formula[i];
            var position = i + 1;

            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (char.IsAsciiDigit(c))
            {
                var start = i;
                while (i < formula.Length && char.IsAsciiDigit(formula[i])) i++;
                if (i + 1 < formula.Length && formula[i] == '.' && char.IsAsciiDigit(formula[i + 1]))
                {
                    i++;
                    while (i < formula.Length && char.IsAsciiDigit(formula[i])) i++;
                }
                tokens.Add(new Token(TokenKind.Number, formula[start..i], position));
            }
            else if (char.IsAsciiLetter(c) || c == '_')
            {
                var start = i;
                while (i < formula.Length && (char.IsAsciiLetterOrDigit(formula[i]) || formula[i] == '_')) i++;
                tokens.Add(new Token(TokenKind.Identifier, formula[start..i], position));
            }
            else if (c == '"')
            {
                // Text literal. A quote inside the text is written twice: "say ""hi""" → say "hi"
                var text = new StringBuilder();
                i++;
                while (true)
                {
                    if (i >= formula.Length)
                        throw FormulaErrors.Invalid($"Unterminated text starting at position {position}.");
                    if (formula[i] == '"')
                    {
                        if (i + 1 < formula.Length && formula[i + 1] == '"')
                        {
                            text.Append('"');
                            i += 2;
                            continue;
                        }
                        i++;
                        break;
                    }
                    text.Append(formula[i++]);
                }
                tokens.Add(new Token(TokenKind.Text, text.ToString(), position));
            }
            else
            {
                var twoChars = i + 1 < formula.Length ? formula.Substring(i, 2) : "";
                var (kind, length) = twoChars switch
                {
                    "==" => (TokenKind.EqualEqual, 2),
                    "!=" => (TokenKind.NotEqual, 2),
                    "<=" => (TokenKind.LessEqual, 2),
                    ">=" => (TokenKind.GreaterEqual, 2),
                    _ => c switch
                    {
                        '+' => (TokenKind.Plus, 1),
                        '-' => (TokenKind.Minus, 1),
                        '*' => (TokenKind.Star, 1),
                        '/' => (TokenKind.Slash, 1),
                        '%' => (TokenKind.Percent, 1),
                        '^' => (TokenKind.Caret, 1),
                        '<' => (TokenKind.Less, 1),
                        '>' => (TokenKind.Greater, 1),
                        '(' => (TokenKind.LeftParen, 1),
                        ')' => (TokenKind.RightParen, 1),
                        ',' => (TokenKind.Comma, 1),
                        '=' => throw FormulaErrors.Invalid($"Unexpected '=' at position {position}. Use '==' to compare."),
                        _ => throw FormulaErrors.Invalid($"Unexpected character '{c}' at position {position}."),
                    },
                };
                tokens.Add(new Token(kind, formula.Substring(i, length), position));
                i += length;
            }
        }

        tokens.Add(new Token(TokenKind.End, "", formula.Length + 1));
        return tokens;
    }
}
