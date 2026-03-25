namespace Excelst.Compiler;

public sealed class ParseException(string message) : Exception(message);

public sealed class Parser(IReadOnlyList<Token> tokens)
{
    private int _pos = 0;

    private Token Current   => tokens[_pos];
    private Token LookAhead => _pos + 1 < tokens.Count ? tokens[_pos + 1] : tokens[^1];

    private Token Consume()
    {
        var t = tokens[_pos];
        if (t.Kind != TokenKind.Eof) _pos++;
        return t;
    }

    private Token Expect(TokenKind kind)
    {
        if (Current.Kind != kind)
            throw new ParseException(
                $"Attendu {kind} mais trouvé '{Current.Value}' " +
                $"({Current.Kind}) à la ligne {Current.Line}, colonne {Current.Column}");
        return Consume();
    }

    private Token ExpectIdentifier(string name)
    {
        if (Current.Kind != TokenKind.Identifier || Current.Value != name)
            throw new ParseException(
                $"Attendu '{name}' mais trouvé '{Current.Value}' " +
                $"à la ligne {Current.Line}, colonne {Current.Column}");
        return Consume();
    }

    // ── Top-level ─────────────────────────────────────────────────────────────

    public Programme Parse()
    {
        var statements = new List<Statement>();
        while (Current.Kind != TokenKind.Eof)
            statements.Add(ParseStatement(insideSheet: false));
        return new Programme(statements);
    }

    private Statement ParseStatement(bool insideSheet)
    {
        if (Current.Kind != TokenKind.Identifier)
            throw new ParseException(
                $"Instruction attendue mais trouvé '{Current.Value}' " +
                $"à la ligne {Current.Line}, colonne {Current.Column}");

        // Assignment: name = expr  (lookahead for '=')
        if (LookAhead.Kind == TokenKind.Assign)
            return ParseAssignStatement();

        return Current.Value switch
        {
            "let"   => ParseLetStatement(),
            "var"   => ParseVarStatement(),
            "while" => ParseWhileStatement(insideSheet),
            "sheets" when !insideSheet => ParseSheetsAdd(),
            "sheet"  when !insideSheet => ParseSheetBlock(),
            "cell"   when  insideSheet => ParseCellStatement(),
            _ => throw new ParseException(
                     $"Instruction inconnue '{Current.Value}' " +
                     $"(ligne {Current.Line}, colonne {Current.Column})")
        };
    }

    // ── let name = expr ───────────────────────────────────────────────────────

    private LetStatement ParseLetStatement()
    {
        var sourceToken = ExpectIdentifier("let");
        var nameToken   = Expect(TokenKind.Identifier);
        Expect(TokenKind.Assign);
        var initializer = ParseExpression();
        return new LetStatement(nameToken.Value, initializer, sourceToken);
    }

    // ── var name = expr ───────────────────────────────────────────────────────

    private VarStatement ParseVarStatement()
    {
        var sourceToken = ExpectIdentifier("var");
        var nameToken   = Expect(TokenKind.Identifier);
        Expect(TokenKind.Assign);
        var initializer = ParseExpression();
        return new VarStatement(nameToken.Value, initializer, sourceToken);
    }

    // ── name = expr ───────────────────────────────────────────────────────────

    private AssignStatement ParseAssignStatement()
    {
        var nameToken   = Expect(TokenKind.Identifier);
        Expect(TokenKind.Assign);
        var value = ParseExpression();
        return new AssignStatement(nameToken.Value, value, nameToken);
    }

    // ── while condition { body } ─────────────────────────────────────────────

    private WhileStatement ParseWhileStatement(bool insideSheet)
    {
        var sourceToken = ExpectIdentifier("while");
        var condition   = ParseExpression();
        Expect(TokenKind.LBrace);

        var body = new List<Statement>();
        while (Current.Kind != TokenKind.RBrace && Current.Kind != TokenKind.Eof)
            body.Add(ParseStatement(insideSheet));

        Expect(TokenKind.RBrace);
        return new WhileStatement(condition, body, sourceToken);
    }

    // ── sheets.add("name", after: "other") ───────────────────────────────────

    private SheetsAddStatement ParseSheetsAdd()
    {
        var sourceToken = ExpectIdentifier("sheets");
        Expect(TokenKind.Dot);
        ExpectIdentifier("add");
        Expect(TokenKind.LParen);

        var nameToken = Expect(TokenKind.String);
        string? after = null;

        if (Current.Kind == TokenKind.Comma)
        {
            Consume(); // ','
            var paramName = Expect(TokenKind.Identifier);
            if (paramName.Value != "after")
                throw new ParseException(
                    $"Paramètre inconnu '{paramName.Value}' pour sheets.add() " +
                    $"à la ligne {paramName.Line}, colonne {paramName.Column}");
            Expect(TokenKind.Colon);
            after = Expect(TokenKind.String).Value;
        }

        Expect(TokenKind.RParen);
        return new SheetsAddStatement(nameToken.Value, after, sourceToken);
    }

    // ── sheet("name", { ... }) ────────────────────────────────────────────────

    private SheetBlock ParseSheetBlock()
    {
        var sourceToken = ExpectIdentifier("sheet");
        Expect(TokenKind.LParen);
        var nameToken = Expect(TokenKind.String);
        Expect(TokenKind.Comma);
        Expect(TokenKind.LBrace);

        var inner = new List<Statement>();
        while (Current.Kind != TokenKind.RBrace && Current.Kind != TokenKind.Eof)
            inner.Add(ParseStatement(insideSheet: true));

        Expect(TokenKind.RBrace);
        Expect(TokenKind.RParen);

        return new SheetBlock(nameToken.Value, inner, sourceToken);
    }

    // ── cell(address_expr, value_expr) ────────────────────────────────────────

    private CellStatement ParseCellStatement()
    {
        var sourceToken = ExpectIdentifier("cell");
        Expect(TokenKind.LParen);
        var address = ParseExpression();
        Expect(TokenKind.Comma);
        var value = ParseExpression();
        Expect(TokenKind.RParen);
        return new CellStatement(address, value, sourceToken);
    }

    // ── Expression precedence ─────────────────────────────────────────────────
    // ParseExpression → ParseComparison → ParseAddition → ParseMultiplication
    //                 → ParseUnary → ParsePrimary

    private Expression ParseExpression() => ParseComparison();

    private Expression ParseComparison()
    {
        var left = ParseAddition();
        while (Current.Kind is TokenKind.Less or TokenKind.Greater
                              or TokenKind.LessEq or TokenKind.GreaterEq
                              or TokenKind.EqEq or TokenKind.BangEq)
        {
            var opToken = Consume();
            var op = opToken.Kind switch
            {
                TokenKind.Less      => BinaryOp.Lt,
                TokenKind.Greater   => BinaryOp.Gt,
                TokenKind.LessEq    => BinaryOp.LtEq,
                TokenKind.GreaterEq => BinaryOp.GtEq,
                TokenKind.EqEq      => BinaryOp.EqEq,
                TokenKind.BangEq    => BinaryOp.NotEq,
                _ => throw new InvalidOperationException()
            };
            left = new BinaryExpr(left, op, ParseAddition(), opToken);
        }
        return left;
    }

    private Expression ParseAddition()
    {
        var left = ParseMultiplication();
        while (Current.Kind is TokenKind.Plus or TokenKind.Minus)
        {
            var opToken = Consume();
            var op = opToken.Kind == TokenKind.Plus ? BinaryOp.Add : BinaryOp.Sub;
            left = new BinaryExpr(left, op, ParseMultiplication(), opToken);
        }
        return left;
    }

    private Expression ParseMultiplication()
    {
        var left = ParseUnary();
        while (Current.Kind is TokenKind.Star or TokenKind.Slash)
        {
            var opToken = Consume();
            var op = opToken.Kind == TokenKind.Star ? BinaryOp.Mul : BinaryOp.Div;
            left = new BinaryExpr(left, op, ParseUnary(), opToken);
        }
        return left;
    }

    private Expression ParseUnary()
    {
        if (Current.Kind == TokenKind.Minus)
        {
            var opToken = Consume();
            return new UnaryExpr(UnaryOp.Neg, ParseUnary(), opToken);
        }
        return ParsePrimary();
    }

    private Expression ParsePrimary()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.String:
                Consume();
                return new LiteralExpr(new StringValue(token.Value), token);
            case TokenKind.Integer:
                Consume();
                return new LiteralExpr(new IntegerValue(long.Parse(token.Value)), token);
            case TokenKind.Float:
                Consume();
                return new LiteralExpr(
                    new FloatValue(double.Parse(token.Value,
                        System.Globalization.CultureInfo.InvariantCulture)), token);
            case TokenKind.LParen:
                return ParseArrayOrParenExpr();
            case TokenKind.Identifier:
                return ParseIdentifierExpr();
            default:
                throw new ParseException(
                    $"Expression attendue mais trouvé '{token.Value}' " +
                    $"à la ligne {token.Line}, colonne {token.Column}");
        }
    }

    // identifier        → VariableExpr
    // identifier.at(n)  → ArrayAccessExpr
    private Expression ParseIdentifierExpr()
    {
        var nameToken = Expect(TokenKind.Identifier);

        if (Current.Kind != TokenKind.Dot)
            return new VariableExpr(nameToken.Value, nameToken);

        Consume(); // '.'
        ExpectIdentifier("at");
        Expect(TokenKind.LParen);
        var index = ParseExpression();
        Expect(TokenKind.RParen);

        return new ArrayAccessExpr(nameToken.Value, index, nameToken);
    }

    // ()       → ArrayLiteralExpr([])
    // (x,)     → ArrayLiteralExpr([x])
    // (x, y)   → ArrayLiteralExpr([x, y])
    // (x)      → x  (parenthesized expression, not an array)
    private Expression ParseArrayOrParenExpr()
    {
        var sourceToken = Expect(TokenKind.LParen);

        if (Current.Kind == TokenKind.RParen)
        {
            Consume();
            return new ArrayLiteralExpr([], sourceToken);
        }

        var first = ParseExpression();

        if (Current.Kind == TokenKind.RParen)
        {
            Consume();
            return first; // (x) == x
        }

        Expect(TokenKind.Comma);

        if (Current.Kind == TokenKind.RParen)
        {
            Consume();
            return new ArrayLiteralExpr([first], sourceToken); // (x,)
        }

        var items = new List<Expression> { first };
        while (true)
        {
            items.Add(ParseExpression());
            if (Current.Kind != TokenKind.Comma) break;
            Consume(); // ','
            if (Current.Kind == TokenKind.RParen) break; // trailing comma
        }

        Expect(TokenKind.RParen);
        return new ArrayLiteralExpr(items, sourceToken);
    }
}
