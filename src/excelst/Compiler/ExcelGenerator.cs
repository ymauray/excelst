using ClosedXML.Excel;

namespace Excelst.Compiler;

public sealed class GeneratorException(string message) : Exception(message);

public sealed class ExcelGenerator
{
    private readonly XLWorkbook _workbook = new();
    private readonly HashSet<string> _declaredSheets = new();
    private readonly Dictionary<string, Value> _variables = new();

    public XLWorkbook Generate(Programme programme)
    {
        foreach (var statement in programme.Statements)
            Execute(statement);
        return _workbook;
    }

    // ── Dispatch ──────────────────────────────────────────────────────────────

    private void Execute(TopLevelStatement stmt)
    {
        switch (stmt)
        {
            case LetStatement let:
                ExecuteLet(let);
                break;
            case SheetsAddStatement add:
                ExecuteSheetsAdd(add);
                break;
            case SheetBlock block:
                ExecuteSheetBlock(block);
                break;
            default:
                throw new GeneratorException(
                    $"Instruction non supportée à la ligne {stmt.SourceToken.Line}");
        }
    }

    // ── let name = value ─────────────────────────────────────────────────────

    private void ExecuteLet(LetStatement stmt)
    {
        if (_variables.ContainsKey(stmt.Name))
            throw new GeneratorException(
                $"La variable '{stmt.Name}' est déjà définie (ligne {stmt.SourceToken.Line})");

        _variables[stmt.Name] = ResolveEager(stmt.Value, stmt.SourceToken);
    }

    // ── sheets.add("name", after: "other") ───────────────────────────────────

    private void ExecuteSheetsAdd(SheetsAddStatement stmt)
    {
        if (stmt.After is null)
        {
            _workbook.AddWorksheet(stmt.Name);
        }
        else
        {
            if (!_workbook.TryGetWorksheet(stmt.After, out var refSheet))
                throw new GeneratorException(
                    $"sheets.add : la feuille '{stmt.After}' (after:) n'existe pas " +
                    $"(ligne {stmt.SourceToken.Line})");

            var ws = _workbook.AddWorksheet(stmt.Name);
            ws.Position = refSheet.Position + 1;
        }

        _declaredSheets.Add(stmt.Name);
    }

    // ── sheet("name", { ... }) ────────────────────────────────────────────────

    private void ExecuteSheetBlock(SheetBlock block)
    {
        if (!_declaredSheets.Contains(block.Name))
            throw new GeneratorException(
                $"sheet : la feuille '{block.Name}' n'a pas été déclarée avec sheets.add() " +
                $"(ligne {block.SourceToken.Line})");

        if (!_workbook.TryGetWorksheet(block.Name, out var ws))
            throw new GeneratorException(
                $"sheet : feuille '{block.Name}' introuvable dans le classeur " +
                $"(ligne {block.SourceToken.Line})");

        foreach (var inner in block.Statements)
            ExecuteInner(ws, inner);
    }

    // ── Inner statements ──────────────────────────────────────────────────────

    private void ExecuteInner(IXLWorksheet ws, InnerStatement stmt)
    {
        switch (stmt)
        {
            case CellStatement cell:
                ExecuteCell(ws, cell);
                break;
            default:
                throw new GeneratorException(
                    $"Instruction non supportée à la ligne {stmt.SourceToken.Line}");
        }
    }

    // ── cell("A1", value) ─────────────────────────────────────────────────────

    private void ExecuteCell(IXLWorksheet ws, CellStatement stmt)
    {
        ws.Cell(stmt.Address).Value = Resolve(stmt.Value, stmt.SourceToken) switch
        {
            StringValue  s => XLCellValue.FromObject(s.Content),
            IntegerValue i => XLCellValue.FromObject(i.Content),
            FloatValue   f => XLCellValue.FromObject(f.Content),
            ArrayValue   _ => throw new GeneratorException(
                     $"Impossible d'écrire un tableau dans la cellule '{stmt.Address}' " +
                     $"(ligne {stmt.SourceToken.Line})"),
            _ => throw new GeneratorException(
                     $"Type de valeur non supporté à la ligne {stmt.SourceToken.Line}")
        };
    }

    // ── Résolution des valeurs ────────────────────────────────────────────────

    // Résolution eager : résout récursivement les items d'un tableau à la définition.
    private Value ResolveEager(Value value, Token sourceToken)
    {
        var resolved = Resolve(value, sourceToken);
        return resolved is ArrayValue arr
            ? new ArrayValue(arr.Items.Select(item => ResolveEager(item, sourceToken)).ToList())
            : resolved;
    }

    private Value Resolve(Value value, Token sourceToken) => value switch
    {
        VariableValue v when _variables.TryGetValue(v.Name, out var inner)
            => Resolve(inner, sourceToken),
        VariableValue v
            => throw new GeneratorException(
                   $"Variable '{v.Name}' non définie (ligne {sourceToken.Line})"),
        ArrayAccessValue a
            => ResolveArrayAccess(a, sourceToken),
        _ => value
    };

    private Value ResolveArrayAccess(ArrayAccessValue access, Token sourceToken)
    {
        if (!_variables.TryGetValue(access.Name, out var val))
            throw new GeneratorException(
                $"Variable '{access.Name}' non définie (ligne {sourceToken.Line})");

        if (Resolve(val, sourceToken) is not ArrayValue arr)
            throw new GeneratorException(
                $"'{access.Name}' n'est pas un tableau (ligne {sourceToken.Line})");

        if (access.Index < 0 || access.Index >= arr.Items.Count)
            throw new GeneratorException(
                $"Index {access.Index} hors limites pour '{access.Name}' " +
                $"(taille : {arr.Items.Count}, ligne {sourceToken.Line})");

        return Resolve(arr.Items[access.Index], sourceToken);
    }
}
