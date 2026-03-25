using ClosedXML.Excel;
using Excelst.Compiler;

namespace Excelst.Tests.Compiler;

public class VariableTests
{
    private static Programme Parse(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        return new Parser(tokens).Parse();
    }

    private static XLWorkbook Generate(string source)
    {
        var programme = Parse(source);
        return new ExcelGenerator().Generate(programme);
    }

    // ── Parser ────────────────────────────────────────────────────────────────

    [Fact]
    public void Let_chaine_produit_LetStatement()
    {
        var prog = Parse("let x = \"Bonjour\"");
        var stmt = Assert.IsType<LetStatement>(Assert.Single(prog.Statements));
        Assert.Equal("x", stmt.Name);
        Assert.Equal("Bonjour", Assert.IsType<StringValue>(stmt.Value).Content);
    }

    [Fact]
    public void Let_entier_produit_LetStatement()
    {
        var prog = Parse("let n = 42");
        var stmt = Assert.IsType<LetStatement>(Assert.Single(prog.Statements));
        Assert.Equal(42L, Assert.IsType<IntegerValue>(stmt.Value).Content);
    }

    [Fact]
    public void Let_decimal_produit_LetStatement()
    {
        var prog = Parse("let pi = 3.14");
        var stmt = Assert.IsType<LetStatement>(Assert.Single(prog.Statements));
        Assert.Equal(3.14, Assert.IsType<FloatValue>(stmt.Value).Content);
    }

    [Fact]
    public void Cell_avec_variable_produit_VariableValue()
    {
        var prog = Parse("sheets.add(\"A\")\nsheet(\"A\", { cell(\"A1\", x) })");
        var block = Assert.IsType<SheetBlock>(prog.Statements[1]);
        var cell = Assert.IsType<CellStatement>(Assert.Single(block.Statements));
        Assert.Equal("x", Assert.IsType<VariableValue>(cell.Value).Name);
    }

    // ── Générateur ────────────────────────────────────────────────────────────

    [Fact]
    public void Variable_chaine_ecrite_dans_cellule()
    {
        using var wb = Generate("""
            let titre = "Bonjour"
            sheets.add("A")
            sheet("A", { cell("A1", titre) })
            """);
        Assert.Equal("Bonjour", wb.Worksheet("A").Cell("A1").GetString());
    }

    [Fact]
    public void Variable_entiere_ecrite_dans_cellule()
    {
        using var wb = Generate("""
            let n = 99
            sheets.add("A")
            sheet("A", { cell("A1", n) })
            """);
        Assert.Equal(99.0, wb.Worksheet("A").Cell("A1").GetDouble());
    }

    [Fact]
    public void Variable_decimale_ecrite_dans_cellule()
    {
        using var wb = Generate("""
            let pi = 3.1415927
            sheets.add("A")
            sheet("A", { cell("A1", pi) })
            """);
        Assert.Equal(3.1415927, wb.Worksheet("A").Cell("A1").GetDouble(), precision: 6);
    }

    [Fact]
    public void Variable_utilisee_dans_plusieurs_cellules()
    {
        using var wb = Generate("""
            let val = "Copié"
            sheets.add("A")
            sheet("A", {
                cell("A1", val)
                cell("A2", val)
                cell("A3", val)
            })
            """);
        Assert.Equal("Copié", wb.Worksheet("A").Cell("A1").GetString());
        Assert.Equal("Copié", wb.Worksheet("A").Cell("A2").GetString());
        Assert.Equal("Copié", wb.Worksheet("A").Cell("A3").GetString());
    }

    [Fact]
    public void Variable_non_definie_leve_exception()
    {
        Assert.Throws<GeneratorException>(() => Generate("""
            sheets.add("A")
            sheet("A", { cell("A1", fantome) })
            """));
    }

    [Fact]
    public void Variable_definie_apres_usage_leve_exception()
    {
        Assert.Throws<GeneratorException>(() => Generate("""
            sheets.add("A")
            sheet("A", { cell("A1", x) })
            let x = 42
            """));
    }

    [Fact]
    public void Redeclaration_leve_exception()
    {
        Assert.Throws<GeneratorException>(() => Generate("""
            let x = 42
            let x = 99
            """));
    }
}
