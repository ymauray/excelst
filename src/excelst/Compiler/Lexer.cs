namespace Excelst.Compiler;

public sealed class LexerException(string message) : Exception(message);

public sealed class Lexer(string source)
{
    private int _pos = 0;
    private int _line = 1;
    private int _col = 1;

    private char Current => _pos < source.Length ? source[_pos] : '\0';
    private char Peek    => _pos + 1 < source.Length ? source[_pos + 1] : '\0';
    private bool IsAtEnd => _pos >= source.Length;

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (!IsAtEnd)
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd) break;

            var (line, col) = (_line, _col);
            var ch = Current;

            if      (ch == '"')                        tokens.Add(ReadString(line, col));
            else if (char.IsDigit(ch))                 tokens.Add(ReadNumber(line, col));
            else if (char.IsLetter(ch) || ch == '_')   tokens.Add(ReadIdentifier(line, col));
            else if (ch == '<' && Peek == '=')         tokens.Add(ReadTwo(TokenKind.LessEq,    "<=", line, col));
            else if (ch == '>' && Peek == '=')         tokens.Add(ReadTwo(TokenKind.GreaterEq, ">=", line, col));
            else if (ch == '=' && Peek == '=')         tokens.Add(ReadTwo(TokenKind.EqEq,      "==", line, col));
            else if (ch == '!' && Peek == '=')         tokens.Add(ReadTwo(TokenKind.BangEq,    "!=", line, col));
            else
            {
                var kind = ch switch
                {
                    '=' => TokenKind.Assign,
                    '<' => TokenKind.Less,
                    '>' => TokenKind.Greater,
                    '+' => TokenKind.Plus,
                    '-' => TokenKind.Minus,
                    '*' => TokenKind.Star,
                    '/' => TokenKind.Slash,
                    '.' => TokenKind.Dot,
                    '(' => TokenKind.LParen,
                    ')' => TokenKind.RParen,
                    '{' => TokenKind.LBrace,
                    '}' => TokenKind.RBrace,
                    ',' => TokenKind.Comma,
                    ':' => TokenKind.Colon,
                    _ => throw new LexerException(
                             $"Caractère inattendu '{ch}' à la ligne {_line}, colonne {_col}")
                };
                tokens.Add(new Token(kind, ch.ToString(), line, col));
                Advance();
            }
        }

        tokens.Add(new Token(TokenKind.Eof, "", _line, _col));
        return tokens;
    }

    private Token ReadTwo(TokenKind kind, string lexeme, int line, int col)
    {
        Advance(); Advance();
        return new Token(kind, lexeme, line, col);
    }

    private void Advance()
    {
        if (_pos < source.Length && source[_pos] == '\n') { _line++; _col = 1; }
        else _col++;
        _pos++;
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd)
        {
            if (char.IsWhiteSpace(Current))
                Advance();
            else if (Current == '/' && Peek == '/')
                while (!IsAtEnd && Current != '\n') Advance();
            else
                break;
        }
    }

    private Token ReadString(int line, int col)
    {
        Advance(); // "
        var sb = new System.Text.StringBuilder();
        while (!IsAtEnd && Current != '"')
        {
            if (Current == '\n')
                throw new LexerException(
                    $"Chaîne non terminée débutant à la ligne {line}, colonne {col}");
            sb.Append(Current);
            Advance();
        }
        if (IsAtEnd)
            throw new LexerException(
                $"Chaîne non terminée débutant à la ligne {line}, colonne {col}");
        Advance(); // "
        return new Token(TokenKind.String, sb.ToString(), line, col);
    }

    private Token ReadNumber(int line, int col)
    {
        var sb = new System.Text.StringBuilder();
        while (!IsAtEnd && char.IsDigit(Current)) { sb.Append(Current); Advance(); }
        if (!IsAtEnd && Current == '.' && char.IsDigit(Peek))
        {
            sb.Append(Current); Advance();
            while (!IsAtEnd && char.IsDigit(Current)) { sb.Append(Current); Advance(); }
            return new Token(TokenKind.Float, sb.ToString(), line, col);
        }
        return new Token(TokenKind.Integer, sb.ToString(), line, col);
    }

    private Token ReadIdentifier(int line, int col)
    {
        var sb = new System.Text.StringBuilder();
        while (!IsAtEnd && (char.IsLetterOrDigit(Current) || Current == '_'))
        {
            sb.Append(Current); Advance();
        }
        return new Token(TokenKind.Identifier, sb.ToString(), line, col);
    }
}
