namespace Excelst.Compiler;

public sealed class LexerException(string message) : Exception(message);

public sealed class Lexer(string source)
{
    private int _pos = 0;
    private int _line = 1;
    private int _col = 1;

    private char Current => _pos < source.Length ? source[_pos] : '\0';
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

            if (ch == '"')
                tokens.Add(ReadString(line, col));
            else if (char.IsDigit(ch))
                tokens.Add(ReadNumber(line, col));
            else if (char.IsLetter(ch) || ch == '_')
                tokens.Add(ReadIdentifier(line, col));
            else
            {
                var kind = ch switch
                {
                    '=' => TokenKind.Equals,
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

    private void Advance()
    {
        if (_pos < source.Length && source[_pos] == '\n')
        {
            _line++;
            _col = 1;
        }
        else
        {
            _col++;
        }
        _pos++;
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd)
        {
            if (char.IsWhiteSpace(Current))
                Advance();
            else if (Current == '/' && _pos + 1 < source.Length && source[_pos + 1] == '/')
                while (!IsAtEnd && Current != '\n')
                    Advance();
            else
                break;
        }
    }

    private Token ReadString(int line, int col)
    {
        Advance(); // consume opening "
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
        Advance(); // consume closing "
        return new Token(TokenKind.String, sb.ToString(), line, col);
    }

    private Token ReadNumber(int line, int col)
    {
        var sb = new System.Text.StringBuilder();
        while (!IsAtEnd && char.IsDigit(Current))
        {
            sb.Append(Current);
            Advance();
        }
        if (!IsAtEnd && Current == '.' && _pos + 1 < source.Length && char.IsDigit(source[_pos + 1]))
        {
            sb.Append(Current);
            Advance();
            while (!IsAtEnd && char.IsDigit(Current))
            {
                sb.Append(Current);
                Advance();
            }
            return new Token(TokenKind.Float, sb.ToString(), line, col);
        }
        return new Token(TokenKind.Integer, sb.ToString(), line, col);
    }

    private Token ReadIdentifier(int line, int col)
    {
        var sb = new System.Text.StringBuilder();
        while (!IsAtEnd && (char.IsLetterOrDigit(Current) || Current == '_'))
        {
            sb.Append(Current);
            Advance();
        }
        return new Token(TokenKind.Identifier, sb.ToString(), line, col);
    }
}
