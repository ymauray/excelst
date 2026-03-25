using ClosedXML.Excel;
using Excelst.Compiler;

namespace Excelst.Tests.Compiler;

public class GeneratorTests
{
    private static XLWorkbook Generate(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        var programme = new Parser(tokens).Parse();
        return new ExcelGenerator().Generate(programme);
    }

    // ── sheets.add ────────────────────────────────────────────────────────────

    [Fact]
    public void SheetsAdd_cree_une_feuille()
    {
        using var wb = Generate("sheets.add(\"Budget\")");
        Assert.True(wb.TryGetWorksheet("Budget", out _));
    }

    [Fact]
    public void SheetsAdd_plusieurs_feuilles_dans_lordre()
    {
        using var wb = Generate("""
            sheets.add("F1")
            sheets.add("F2")
            sheets.add("F3")
            """);

        Assert.Equal("F1", wb.Worksheet(1).Name);
        Assert.Equal("F2", wb.Worksheet(2).Name);
        Assert.Equal("F3", wb.Worksheet(3).Name);
    }

    [Fact]
    public void SheetsAdd_after_insere_a_la_bonne_position()
    {
        using var wb = Generate("""
            sheets.add("F1")
            sheets.add("F3")
            sheets.add("F2", after: "F1")
            """);

        Assert.Equal("F1", wb.Worksheet(1).Name);
        Assert.Equal("F2", wb.Worksheet(2).Name);
        Assert.Equal("F3", wb.Worksheet(3).Name);
    }

    [Fact]
    public void SheetsAdd_after_feuille_inexistante_leve_exception()
    {
        Assert.Throws<GeneratorException>(() =>
            Generate("sheets.add(\"X\", after: \"Fantôme\")"));
    }

    // ── sheet block ───────────────────────────────────────────────────────────

    [Fact]
    public void SheetBlock_sans_declaration_leve_exception()
    {
        Assert.Throws<GeneratorException>(() =>
            Generate("sheet(\"Fantôme\", { })"));
    }

    [Fact]
    public void SheetBlock_vide_ne_modifie_pas_la_feuille()
    {
        using var wb = Generate("""
            sheets.add("A")
            sheet("A", {})
            """);

        Assert.True(wb.TryGetWorksheet("A", out var ws));
        Assert.True(ws!.CellsUsed().Count() == 0);
    }

    // ── cell ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Cell_ecrit_une_chaine()
    {
        using var wb = Generate("""
            sheets.add("A")
            sheet("A", { cell("A1", "Bonjour") })
            """);

        Assert.Equal("Bonjour", wb.Worksheet("A").Cell("A1").GetString());
    }

    [Fact]
    public void Cell_ecrit_un_entier()
    {
        using var wb = Generate("""
            sheets.add("A")
            sheet("A", { cell("B2", 42) })
            """);

        Assert.Equal(42.0, wb.Worksheet("A").Cell("B2").GetDouble());
    }

    [Fact]
    public void Cell_ecrit_un_decimal()
    {
        using var wb = Generate("""
            sheets.add("A")
            sheet("A", { cell("C3", 3.1415927) })
            """);

        Assert.Equal(3.1415927, wb.Worksheet("A").Cell("C3").GetDouble(), precision: 6);
    }

    [Fact]
    public void Cell_plusieurs_valeurs_dans_plusieurs_feuilles()
    {
        using var wb = Generate("""
            sheets.add("F1")
            sheets.add("F2")
            sheet("F1", { cell("A1", "un") })
            sheet("F2", { cell("A1", "deux") })
            """);

        Assert.Equal("un",   wb.Worksheet("F1").Cell("A1").GetString());
        Assert.Equal("deux", wb.Worksheet("F2").Cell("A1").GetString());
    }
}
