namespace Excelst.Compiler;

public sealed class ParseException(string message) : Exception(message);

public sealed class Parser(IReadOnlyList<Token> tokens)
{
    private int _pos = 0;

    private Token Current => tokens[_pos];

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
        var statements = new List<TopLevelStatement>();
        while (Current.Kind != TokenKind.Eof)
            statements.Add(ParseTopLevelStatement());
        return new Programme(statements);
    }

    private TopLevelStatement ParseTopLevelStatement()
    {
        if (Current.Kind != TokenKind.Identifier)
            throw new ParseException(
                $"Instruction attendue mais trouvé '{Current.Value}' " +
                $"à la ligne {Current.Line}, colonne {Current.Column}");

        return Current.Value switch
        {
            "let"    => ParseLetStatement(),
            "sheets" => ParseSheetsAdd(),
            "sheet"  => ParseSheetBlock(),
            _ => throw new ParseException(
                     $"Instruction inconnue '{Current.Value}' " +
                     $"à la ligne {Current.Line}, colonne {Current.Column}")
        };
    }

    // ── let name = value ─────────────────────────────────────────────────────

    private LetStatement ParseLetStatement()
    {
        var sourceToken = ExpectIdentifier("let");
        var nameToken = Expect(TokenKind.Identifier);
        Expect(TokenKind.Equals);
        var value = ParseValue();
        return new LetStatement(nameToken.Value, value, sourceToken);
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

        var inner = new List<InnerStatement>();
        while (Current.Kind != TokenKind.RBrace && Current.Kind != TokenKind.Eof)
            inner.Add(ParseInnerStatement());

        Expect(TokenKind.RBrace);
        Expect(TokenKind.RParen);

        return new SheetBlock(nameToken.Value, inner, sourceToken);
    }

    // ── Inner statements ──────────────────────────────────────────────────────

    private InnerStatement ParseInnerStatement()
    {
        if (Current.Kind == TokenKind.Identifier && Current.Value == "cell")
            return ParseCellStatement();

        throw new ParseException(
            $"Instruction inconnue '{Current.Value}' dans un bloc sheet " +
            $"à la ligne {Current.Line}, colonne {Current.Column}");
    }

    // ── cell("A1", value) ─────────────────────────────────────────────────────

    private CellStatement ParseCellStatement()
    {
        var sourceToken = ExpectIdentifier("cell");
        Expect(TokenKind.LParen);
        var address = Expect(TokenKind.String);
        Expect(TokenKind.Comma);
        var value = ParseValue();
        Expect(TokenKind.RParen);

        return new CellStatement(address.Value, value, sourceToken);
    }

    // ── Values ────────────────────────────────────────────────────────────────

    // identifier        → VariableValue
    // identifier.at(n)  → ArrayAccessValue
    private Value ParseIdentifierValue()
    {
        var nameToken = Expect(TokenKind.Identifier);

        if (Current.Kind != TokenKind.Dot)
            return new VariableValue(nameToken.Value);

        Consume(); // '.'
        ExpectIdentifier("at");
        Expect(TokenKind.LParen);
        var indexToken = Expect(TokenKind.Integer);
        Expect(TokenKind.RParen);

        return new ArrayAccessValue(nameToken.Value, int.Parse(indexToken.Value));
    }

    // ()       → ArrayValue([])
    // (x,)     → ArrayValue([x])
    // (x, y)   → ArrayValue([x, y])
    // (x)      → x  (expression parenthésée, pas un tableau)
    private Value ParseArrayLiteral()
    {
        Expect(TokenKind.LParen);

        if (Current.Kind == TokenKind.RParen)
        {
            Consume();
            return new ArrayValue([]);
        }

        var first = ParseValue();

        if (Current.Kind == TokenKind.RParen)
        {
            Consume();
            return first; // (x) == x
        }

        Expect(TokenKind.Comma);

        if (Current.Kind == TokenKind.RParen)
        {
            Consume();
            return new ArrayValue([first]); // (x,)
        }

        var items = new List<Value> { first };
        while (true)
        {
            items.Add(ParseValue());
            if (Current.Kind != TokenKind.Comma) break;
            Consume(); // ','
            if (Current.Kind == TokenKind.RParen) break; // trailing comma
        }

        Expect(TokenKind.RParen);
        return new ArrayValue(items);
    }

    private Value ParseValue()
    {
        switch (Current.Kind)
        {
            case TokenKind.String:
                return new StringValue(Consume().Value);
            case TokenKind.Integer:
                return new IntegerValue(long.Parse(Consume().Value));
            case TokenKind.Float:
                return new FloatValue(double.Parse(Consume().Value,
                           System.Globalization.CultureInfo.InvariantCulture));
            case TokenKind.LParen:
                return ParseArrayLiteral();
            case TokenKind.Identifier:
                return ParseIdentifierValue();
            default:
                throw new ParseException(
                    $"Valeur attendue (texte, entier, décimal, tableau ou variable) " +
                    $"mais trouvé '{Current.Value}' à la ligne {Current.Line}, colonne {Current.Column}");
        }
    }
}
