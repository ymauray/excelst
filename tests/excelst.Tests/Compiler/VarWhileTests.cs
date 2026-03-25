using ClosedXML.Excel;
using Excelst.Compiler;

namespace Excelst.Tests.Compiler;

public class VarWhileTests
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

    // ── Parser — var ──────────────────────────────────────────────────────────

    [Fact]
    public void Var_produit_VarStatement()
    {
        var prog = Parse("var n = 0");
        var stmt = Assert.IsType<VarStatement>(Assert.Single(prog.Statements));
        Assert.Equal("n", stmt.Name);
        Assert.Equal(0L, Assert.IsType<IntegerValue>(Assert.IsType<LiteralExpr>(stmt.Initializer).Val).Content);
    }

    [Fact]
    public void Assign_produit_AssignStatement()
    {
        var prog = Parse("var n = 0\nn = 1");
        Assert.Equal(2, prog.Statements.Count);
        var assign = Assert.IsType<AssignStatement>(prog.Statements[1]);
        Assert.Equal("n", assign.Name);
        Assert.Equal(1L, Assert.IsType<IntegerValue>(Assert.IsType<LiteralExpr>(assign.Value).Val).Content);
    }

    [Fact]
    public void While_produit_WhileStatement()
    {
        var prog = Parse("var n = 0\nwhile n < 3 { n = n + 1 }");
        var while_ = Assert.IsType<WhileStatement>(prog.Statements[1]);
        Assert.IsType<BinaryExpr>(while_.Condition);
        Assert.Single(while_.Body);
    }

    // ── Générateur — var ──────────────────────────────────────────────────────

    [Fact]
    public void Var_peut_etre_mutee()
    {
        using var wb = Generate("""
            var n = 10
            n = 20
            sheets.add("S")
            sheet("S", { cell("A1", n) })
            """);
        Assert.Equal(20.0, wb.Worksheet("S").Cell("A1").GetDouble());
    }

    [Fact]
    public void Let_immutable_leve_exception_si_mutee()
    {
        Assert.Throws<GeneratorException>(() => Generate("""
            let n = 10
            n = 20
            """));
    }

    // ── Générateur — while ────────────────────────────────────────────────────

    [Fact]
    public void While_incremente_variable()
    {
        using var wb = Generate("""
            var n = 0
            while n < 5 { n = n + 1 }
            sheets.add("S")
            sheet("S", { cell("A1", n) })
            """);
        Assert.Equal(5.0, wb.Worksheet("S").Cell("A1").GetDouble());
    }

    [Fact]
    public void While_ecrit_cellules()
    {
        using var wb = Generate("""
            var i = 1
            sheets.add("S")
            sheet("S", {
                while i < 4 {
                    cell("A" + i, i)
                    i = i + 1
                }
            })
            """);
        Assert.Equal(1.0, wb.Worksheet("S").Cell("A1").GetDouble());
        Assert.Equal(2.0, wb.Worksheet("S").Cell("A2").GetDouble());
        Assert.Equal(3.0, wb.Worksheet("S").Cell("A3").GetDouble());
    }

    [Fact]
    public void While_corps_vide_condition_fausse_ne_boucle_pas()
    {
        using var wb = Generate("""
            var n = 5
            while n < 0 { n = n + 1 }
            sheets.add("S")
            sheet("S", { cell("A1", n) })
            """);
        Assert.Equal(5.0, wb.Worksheet("S").Cell("A1").GetDouble());
    }

    // ── Générateur — expressions ──────────────────────────────────────────────

    [Fact]
    public void Addition_entiers()
    {
        using var wb = Generate("""
            sheets.add("S")
            sheet("S", { cell("A1", 3 + 4) })
            """);
        Assert.Equal(7.0, wb.Worksheet("S").Cell("A1").GetDouble());
    }

    [Fact]
    public void Soustraction_entiers()
    {
        using var wb = Generate("""
            sheets.add("S")
            sheet("S", { cell("A1", 10 - 3) })
            """);
        Assert.Equal(7.0, wb.Worksheet("S").Cell("A1").GetDouble());
    }

    [Fact]
    public void Multiplication_entiers()
    {
        using var wb = Generate("""
            sheets.add("S")
            sheet("S", { cell("A1", 6 * 7) })
            """);
        Assert.Equal(42.0, wb.Worksheet("S").Cell("A1").GetDouble());
    }

    [Fact]
    public void Division_entiers()
    {
        using var wb = Generate("""
            sheets.add("S")
            sheet("S", { cell("A1", 15 / 3) })
            """);
        Assert.Equal(5.0, wb.Worksheet("S").Cell("A1").GetDouble());
    }

    [Fact]
    public void Precedence_multiplication_avant_addition()
    {
        using var wb = Generate("""
            sheets.add("S")
            sheet("S", { cell("A1", 2 + 3 * 4) })
            """);
        Assert.Equal(14.0, wb.Worksheet("S").Cell("A1").GetDouble());
    }

    [Fact]
    public void Concatenation_chaines()
    {
        using var wb = Generate("""
            sheets.add("S")
            sheet("S", { cell("A1", "Bonjour " + "monde") })
            """);
        Assert.Equal("Bonjour monde", wb.Worksheet("S").Cell("A1").GetString());
    }

    [Fact]
    public void Negation_unaire()
    {
        using var wb = Generate("""
            sheets.add("S")
            sheet("S", { cell("A1", -5) })
            """);
        Assert.Equal(-5.0, wb.Worksheet("S").Cell("A1").GetDouble());
    }

    [Fact]
    public void Division_par_zero_leve_exception()
    {
        Assert.Throws<GeneratorException>(() => Generate("""
            sheets.add("S")
            sheet("S", { cell("A1", 1 / 0) })
            """));
    }
}
