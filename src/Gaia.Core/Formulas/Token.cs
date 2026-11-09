namespace Gaia.Core.Formulas;

public enum TokenKind
{
    Number,
    Text,
    Identifier,
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    Caret,
    EqualEqual,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    LeftParen,
    RightParen,
    Comma,
    End,
}

/// <summary>
/// One piece of a formula. <see cref="Text"/> is the source text, except for text literals,
/// where it is the value without the quotes. <see cref="Position"/> is 1-based, for error messages.
/// </summary>
public sealed record Token(TokenKind Kind, string Text, int Position);
