namespace Excelst.Compiler;

public enum TokenKind
{
    Identifier, String, Integer, Float,
    // Assignment
    Assign,         // =
    // Comparison
    EqEq,           // ==
    BangEq,         // !=
    Less,           // <
    Greater,        // >
    LessEq,         // <=
    GreaterEq,      // >=
    // Arithmetic
    Plus,           // +
    Minus,          // -
    Star,           // *
    Slash,          // /
    // Punctuation
    Dot, LParen, RParen, LBrace, RBrace, Comma, Colon,
    Eof,
}

public sealed record Token(TokenKind Kind, string Value, int Line, int Column);
