using ClosedXML.Excel;
using Excelst.Compiler;

namespace Excelst.Tests.Compiler;

public class ArrayTests
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

    // ── Parser — littéraux ────────────────────────────────────────────────────

    [Fact]
    public void Tableau_vide()
    {
        var prog = Parse("let a = ()");
        var stmt = Assert.IsType<LetStatement>(Assert.Single(prog.Statements));
        var arr = Assert.IsType<ArrayLiteralExpr>(stmt.Initializer);
        Assert.Empty(arr.Items);
    }

    [Fact]
    public void Tableau_un_element_virgule_obligatoire()
    {
        var prog = Parse("let a = (42,)");
        var arr = Assert.IsType<ArrayLiteralExpr>(
            Assert.IsType<LetStatement>(Assert.Single(prog.Statements)).Initializer);
        Assert.Single(arr.Items);
        Assert.Equal(42L, Assert.IsType<IntegerValue>(Assert.IsType<LiteralExpr>(arr.Items[0]).Val).Content);
    }

    [Fact]
    public void Tableau_plusieurs_elements()
    {
        var prog = Parse("let a = (1, 2, 3)");
        var arr = Assert.IsType<ArrayLiteralExpr>(
            Assert.IsType<LetStatement>(Assert.Single(prog.Statements)).Initializer);
        Assert.Equal(3, arr.Items.Count);
    }

    [Fact]
    public void Tableau_virgule_finale_acceptee()
    {
        var prog = Parse("let a = (1, 2, 3,)");
        var arr = Assert.IsType<ArrayLiteralExpr>(
            Assert.IsType<LetStatement>(Assert.Single(prog.Statements)).Initializer);
        Assert.Equal(3, arr.Items.Count);
    }

    [Fact]
    public void Tableau_types_mixtes()
    {
        var prog = Parse("let a = (1, \"hello\", 3.14)");
        var arr = Assert.IsType<ArrayLiteralExpr>(
            Assert.IsType<LetStatement>(Assert.Single(prog.Statements)).Initializer);
        Assert.IsType<IntegerValue>(Assert.IsType<LiteralExpr>(arr.Items[0]).Val);
        Assert.IsType<StringValue>(Assert.IsType<LiteralExpr>(arr.Items[1]).Val);
        Assert.IsType<FloatValue>(Assert.IsType<LiteralExpr>(arr.Items[2]).Val);
    }

    [Fact]
    public void Parentheses_sans_virgule_retourne_valeur_directe()
    {
        var prog = Parse("let a = (42)");
        var stmt = Assert.IsType<LetStatement>(Assert.Single(prog.Statements));
        var lit = Assert.IsType<LiteralExpr>(stmt.Initializer); // pas un ArrayLiteralExpr
        Assert.IsType<IntegerValue>(lit.Val);
    }

    // ── Parser — accès .at() ─────────────────────────────────────────────────

    [Fact]
    public void At_produit_ArrayAccessExpr()
    {
        var prog = Parse("sheets.add(\"S\")\nsheet(\"S\", { cell(\"A1\", arr.at(2)) })");
        var block = Assert.IsType<SheetBlock>(prog.Statements[1]);
        var cell = Assert.IsType<CellStatement>(Assert.Single(block.Statements));
        var access = Assert.IsType<ArrayAccessExpr>(cell.Value);
        Assert.Equal("arr", access.Name);
        Assert.Equal(2L, Assert.IsType<IntegerValue>(Assert.IsType<LiteralExpr>(access.Index).Val).Content);
    }

    // ── Générateur ────────────────────────────────────────────────────────────

    [Fact]
    public void At_lit_element_correct()
    {
        using var wb = Generate("""
            let arr = (10, 20, 30)
            sheets.add("S")
            sheet("S", { cell("A1", arr.at(1)) })
            """);
        Assert.Equal(20.0, wb.Worksheet("S").Cell("A1").GetDouble());
    }

    [Fact]
    public void At_premier_element()
    {
        using var wb = Generate("""
            let arr = ("a", "b", "c")
            sheets.add("S")
            sheet("S", { cell("A1", arr.at(0)) })
            """);
        Assert.Equal("a", wb.Worksheet("S").Cell("A1").GetString());
    }

    [Fact]
    public void At_dernier_element()
    {
        using var wb = Generate("""
            let arr = ("a", "b", "c")
            sheets.add("S")
            sheet("S", { cell("A1", arr.at(2)) })
            """);
        Assert.Equal("c", wb.Worksheet("S").Cell("A1").GetString());
    }

    [Fact]
    public void At_element_avec_variable_resolue()
    {
        using var wb = Generate("""
            let val = "contenu"
            let arr = (val,)
            sheets.add("S")
            sheet("S", { cell("A1", arr.at(0)) })
            """);
        Assert.Equal("contenu", wb.Worksheet("S").Cell("A1").GetString());
    }

    [Fact]
    public void At_index_hors_limites_leve_exception()
    {
        Assert.Throws<GeneratorException>(() => Generate("""
            let arr = (1, 2)
            sheets.add("S")
            sheet("S", { cell("A1", arr.at(5)) })
            """));
    }

    [Fact]
    public void At_sur_non_tableau_leve_exception()
    {
        Assert.Throws<GeneratorException>(() => Generate("""
            let x = 42
            sheets.add("S")
            sheet("S", { cell("A1", x.at(0)) })
            """));
    }

    [Fact]
    public void Resolution_eager_tableau_dans_tableau()
    {
        // Les items sont résolus à la définition : inner est résolu avant d'être capturé dans outer
        using var wb = Generate("""
            let inner = ("x", "y")
            let outer = (inner.at(0), inner.at(1))
            sheets.add("S")
            sheet("S", {
                cell("A1", outer.at(0))
                cell("A2", outer.at(1))
            })
            """);
        Assert.Equal("x", wb.Worksheet("S").Cell("A1").GetString());
        Assert.Equal("y", wb.Worksheet("S").Cell("A2").GetString());
    }

    [Fact]
    public void Cell_avec_tableau_direct_leve_exception()
    {
        Assert.Throws<GeneratorException>(() => Generate("""
            let arr = (1, 2)
            sheets.add("S")
            sheet("S", { cell("A1", arr) })
            """));
    }
}
