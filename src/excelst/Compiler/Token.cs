namespace Excelst.Compiler;

public enum TokenKind
{
    Identifier,
    String,
    Integer,
    Float,
    Equals,
    Dot,
    LParen,
    RParen,
    LBrace,
    RBrace,
    Comma,
    Colon,
    Eof,
}

public sealed record Token(
    TokenKind Kind,
    string Value,
    int Line,
    int Column
);
