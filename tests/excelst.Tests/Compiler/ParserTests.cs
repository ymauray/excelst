using Excelst.Compiler;

namespace Excelst.Tests.Compiler;

public class ParserTests
{
    private static Programme Parse(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        return new Parser(tokens).Parse();
    }

    // ── sheets.add ────────────────────────────────────────────────────────────

    [Fact]
    public void SheetsAdd_sans_after()
    {
        var prog = Parse("sheets.add(\"Budget\")");
        var stmt = Assert.Single(prog.Statements);
        var add = Assert.IsType<SheetsAddStatement>(stmt);
        Assert.Equal("Budget", add.Name);
        Assert.Null(add.After);
    }

    [Fact]
    public void SheetsAdd_avec_after()
    {
        var prog = Parse("sheets.add(\"Détails\", after: \"Budget\")");
        var add = Assert.IsType<SheetsAddStatement>(Assert.Single(prog.Statements));
        Assert.Equal("Détails", add.Name);
        Assert.Equal("Budget", add.After);
    }

    [Fact]
    public void SheetsAdd_parametre_inconnu_leve_exception()
    {
        Assert.Throws<ParseException>(() => Parse("sheets.add(\"X\", before: \"Y\")"));
    }

    // ── sheet block ───────────────────────────────────────────────────────────

    [Fact]
    public void SheetBlock_vide()
    {
        var prog = Parse("sheets.add(\"A\")\nsheet(\"A\", {})");
        var block = Assert.IsType<SheetBlock>(prog.Statements[1]);
        Assert.Equal("A", block.Name);
        Assert.Empty(block.Statements);
    }

    [Fact]
    public void SheetBlock_avec_cellules()
    {
        var prog = Parse("""
            sheets.add("A")
            sheet("A", {
                cell("A1", "Bonjour")
                cell("B1", 42)
                cell("C1", 3.14)
            })
            """);

        var block = Assert.IsType<SheetBlock>(prog.Statements[1]);
        Assert.Equal(3, block.Statements.Count);
    }

    // ── cell ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Cell_valeur_chaine()
    {
        var prog = Parse("sheets.add(\"A\")\nsheet(\"A\", { cell(\"A1\", \"Texte\") })");
        var block = Assert.IsType<SheetBlock>(prog.Statements[1]);
        var cell = Assert.IsType<CellStatement>(block.Statements[0]);
        Assert.Equal("A1", Assert.IsType<StringValue>(Assert.IsType<LiteralExpr>(cell.Address).Val).Content);
        Assert.Equal("Texte", Assert.IsType<StringValue>(Assert.IsType<LiteralExpr>(cell.Value).Val).Content);
    }

    [Fact]
    public void Cell_valeur_entiere()
    {
        var prog = Parse("sheets.add(\"A\")\nsheet(\"A\", { cell(\"B2\", 99) })");
        var block = Assert.IsType<SheetBlock>(prog.Statements[1]);
        var cell = Assert.IsType<CellStatement>(block.Statements[0]);
        Assert.Equal(99L, Assert.IsType<IntegerValue>(Assert.IsType<LiteralExpr>(cell.Value).Val).Content);
    }

    [Fact]
    public void Cell_valeur_decimale()
    {
        var prog = Parse("sheets.add(\"A\")\nsheet(\"A\", { cell(\"C3\", 1.5) })");
        var block = Assert.IsType<SheetBlock>(prog.Statements[1]);
        var cell = Assert.IsType<CellStatement>(block.Statements[0]);
        Assert.Equal(1.5, Assert.IsType<FloatValue>(Assert.IsType<LiteralExpr>(cell.Value).Val).Content);
    }

    // ── Erreurs ───────────────────────────────────────────────────────────────

    [Fact]
    public void Instruction_inconnue_leve_exception()
    {
        Assert.Throws<ParseException>(() => Parse("foo.bar()"));
    }

    [Fact]
    public void Instruction_inconnue_dans_bloc_leve_exception()
    {
        Assert.Throws<ParseException>(() => Parse("sheets.add(\"A\")\nsheet(\"A\", { foo(\"A1\") })"));
    }

    [Fact]
    public void Valeur_manquante_leve_exception()
    {
        Assert.Throws<ParseException>(() => Parse("sheets.add(\"A\")\nsheet(\"A\", { cell(\"A1\") })"));
    }

    // ── Programme complet ─────────────────────────────────────────────────────

    [Fact]
    public void Programme_multi_feuilles()
    {
        var prog = Parse("""
            sheets.add("F1")
            sheets.add("F2", after: "F1")
            sheet("F1", { cell("A1", "x") })
            sheet("F2", {})
            """);

        Assert.Equal(4, prog.Statements.Count);
        Assert.IsType<SheetsAddStatement>(prog.Statements[0]);
        Assert.IsType<SheetsAddStatement>(prog.Statements[1]);
        Assert.IsType<SheetBlock>(prog.Statements[2]);
        Assert.IsType<SheetBlock>(prog.Statements[3]);
    }
}
