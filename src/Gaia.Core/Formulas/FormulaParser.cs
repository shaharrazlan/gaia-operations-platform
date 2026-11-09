using System.Globalization;
using Gaia.Core.Errors;

namespace Gaia.Core.Formulas;

/// <summary>
/// Step 2 of 3 (tokenize → parse → evaluate): builds a tree from the tokens with a
/// recursive-descent parser. One method per precedence level, lowest first:
/// <code>
/// comparison     := additive ( (== | != | &lt; | &lt;= | &gt; | &gt;=) additive )?
/// additive       := multiplicative ( (+ | -) multiplicative )*
/// multiplicative := unary ( (* | / | %) unary )*
/// unary          := - unary | power
/// power          := primary ( ^ unary )?          (right-associative: 2^3^2 = 2^(3^2))
/// primary        := number | "text" | A | B | name ( args ) | ( comparison )
/// </code>
/// Only A, B and known functions are accepted, so a formula can't reach anything else (NFR6).
/// </summary>
public sealed class FormulaParser
{
    public const int MaxLength = 500;
    public const int MaxDepth = 50;

    private readonly IReadOnlyList<Token> _tokens;
    private int _index;
    private int _depth;

    private FormulaParser(IReadOnlyList<Token> tokens) => _tokens = tokens;

    public static Node Parse(string formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
            throw Tokenizer.Error("The formula is empty.");
        if (formula.Length > MaxLength)
            throw Tokenizer.Error($"The formula is longer than {MaxLength} characters.");

        var parser = new FormulaParser(Tokenizer.Tokenize(formula));
        var tree = parser.ParseComparison();

        var extra = parser.Current;
        if (extra.Kind != TokenKind.End)
            throw Tokenizer.Error($"Unexpected '{extra.Text}' at position {extra.Position}.");
        return tree;
    }

    private Token Current => _tokens[_index];

    private Token Advance() => _tokens[_index++];

    private bool At(params TokenKind[] kinds) => kinds.Contains(Current.Kind);

    private Node ParseComparison()
    {
        Enter();
        var left = ParseAdditive();
        if (At(TokenKind.EqualEqual, TokenKind.NotEqual, TokenKind.Less, TokenKind.LessEqual, TokenKind.Greater, TokenKind.GreaterEqual))
        {
            var op = Advance();
            left = new BinaryNode(op.Text, left, ParseAdditive(), op.Position);
        }
        Leave();
        return left;
    }

    private Node ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (At(TokenKind.Plus, TokenKind.Minus))
        {
            var op = Advance();
            left = new BinaryNode(op.Text, left, ParseMultiplicative(), op.Position);
        }
        return left;
    }

    private Node ParseMultiplicative()
    {
        var left = ParseUnary();
        while (At(TokenKind.Star, TokenKind.Slash, TokenKind.Percent))
        {
            var op = Advance();
            left = new BinaryNode(op.Text, left, ParseUnary(), op.Position);
        }
        return left;
    }

    private Node ParseUnary()
    {
        if (!At(TokenKind.Minus))
            return ParsePower();

        var minus = Advance();
        Enter();
        var operand = ParseUnary();
        Leave();
        return new UnaryNode(operand, minus.Position);
    }

    private Node ParsePower()
    {
        var left = ParsePrimary();
        if (!At(TokenKind.Caret))
            return left;

        var op = Advance();
        Enter();
        var right = ParseUnary(); // recursion makes ^ right-associative and allows 2 ^ -1
        Leave();
        return new BinaryNode(op.Text, left, right, op.Position);
    }

    private Node ParsePrimary()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Number:
                Advance();
                var number = double.Parse(token.Text, CultureInfo.InvariantCulture);
                if (!double.IsFinite(number))
                    throw Tokenizer.Error($"The number at position {token.Position} is too large.");
                return new NumberNode(number, token.Position);

            case TokenKind.Text:
                Advance();
                return new TextNode(token.Text, token.Position);

            case TokenKind.LeftParen:
                Advance();
                var inner = ParseComparison();
                Expect(TokenKind.RightParen, "')'");
                return inner;

            case TokenKind.Identifier when _tokens[_index + 1].Kind == TokenKind.LeftParen:
                return ParseCall();

            case TokenKind.Identifier when token.Text is "A" or "B":
                Advance();
                return new VariableNode(token.Text, token.Position);

            case TokenKind.Identifier:
                throw Tokenizer.Error($"Unknown name '{token.Text}' at position {token.Position}. Use A or B.");

            default:
                throw Tokenizer.Error($"Expected a value at position {token.Position} but {Describe(token)}.");
        }
    }

    private CallNode ParseCall()
    {
        var name = Advance();
        Advance(); // '('
        var arguments = new List<Node>();
        if (!At(TokenKind.RightParen))
        {
            arguments.Add(ParseComparison());
            while (At(TokenKind.Comma))
            {
                Advance();
                arguments.Add(ParseComparison());
            }
        }
        Expect(TokenKind.RightParen, "')'");
        Functions.CheckCall(name.Text, arguments.Count, name.Position);
        return new CallNode(name.Text, arguments, name.Position);
    }

    private void Expect(TokenKind kind, string description)
    {
        if (!At(kind))
            throw Tokenizer.Error($"Expected {description} at position {Current.Position} but {Describe(Current)}.");
        Advance();
    }

    private static string Describe(Token token) =>
        token.Kind == TokenKind.End ? "the formula ended" : $"found '{token.Text}'";

    // Depth guard: deeply nested input must fail with a clear error, never a StackOverflowException.
    private void Enter()
    {
        if (++_depth > MaxDepth)
            throw Tokenizer.Error($"The formula is nested too deeply (more than {MaxDepth} levels).");
    }

    private void Leave() => _depth--;
}
