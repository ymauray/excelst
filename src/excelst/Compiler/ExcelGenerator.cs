using ClosedXML.Excel;

namespace Excelst.Compiler;

public sealed class GeneratorException(string message) : Exception(message);

public sealed class ExcelGenerator
{
    private readonly XLWorkbook _workbook = new();
    private readonly HashSet<string> _declaredSheets = new();

    // ── Scope ─────────────────────────────────────────────────────────────────

    private sealed class Scope(Scope? parent = null)
    {
        private readonly Dictionary<string, (Value Value, bool Mutable)> _vars = new();

        public void Define(string name, Value value, bool mutable, Token token)
        {
            if (_vars.ContainsKey(name))
                throw new GeneratorException(
                    $"La variable '{name}' est déjà définie (ligne {token.Line})");
            _vars[name] = (value, mutable);
        }

        public Value Get(string name, Token token)
        {
            if (_vars.TryGetValue(name, out var entry)) return entry.Value;
            if (parent is not null) return parent.Get(name, token);
            throw new GeneratorException(
                $"Variable '{name}' non définie (ligne {token.Line})");
        }

        public void Set(string name, Value value, Token token)
        {
            if (_vars.TryGetValue(name, out var entry))
            {
                if (!entry.Mutable)
                    throw new GeneratorException(
                        $"La variable '{name}' est immutable (let) — " +
                        $"utilisez 'var' pour une variable mutable (ligne {token.Line})");
                _vars[name] = (value, true);
                return;
            }
            if (parent is not null) { parent.Set(name, value, token); return; }
            throw new GeneratorException(
                $"Variable '{name}' non définie (ligne {token.Line})");
        }
    }

    // ── Generate ──────────────────────────────────────────────────────────────

    public XLWorkbook Generate(Programme programme)
    {
        var rootScope = new Scope();
        foreach (var stmt in programme.Statements)
            Execute(stmt, rootScope, ws: null);
        return _workbook;
    }

    // ── Dispatch ──────────────────────────────────────────────────────────────

    private void Execute(Statement stmt, Scope scope, IXLWorksheet? ws)
    {
        switch (stmt)
        {
            case LetStatement let:
                scope.Define(let.Name, Evaluate(let.Initializer, scope), mutable: false, let.SourceToken);
                break;
            case VarStatement var_:
                scope.Define(var_.Name, Evaluate(var_.Initializer, scope), mutable: true, var_.SourceToken);
                break;
            case AssignStatement assign:
                scope.Set(assign.Name, Evaluate(assign.Value, scope), assign.SourceToken);
                break;
            case WhileStatement while_:
                ExecuteWhile(while_, scope, ws);
                break;
            case SheetsAddStatement add:
                ExecuteSheetsAdd(add);
                break;
            case SheetBlock block:
                ExecuteSheetBlock(block, scope);
                break;
            case CellStatement cell:
                if (ws is null)
                    throw new GeneratorException(
                        $"Instruction 'cell' hors d'un bloc sheet (ligne {stmt.SourceToken.Line})");
                ExecuteCell(ws, cell, scope);
                break;
            default:
                throw new GeneratorException(
                    $"Instruction non supportée à la ligne {stmt.SourceToken.Line}");
        }
    }

    // ── while condition { body } ─────────────────────────────────────────────

    private void ExecuteWhile(WhileStatement stmt, Scope scope, IXLWorksheet? ws)
    {
        const int maxIterations = 100_000;
        int count = 0;
        while (true)
        {
            if (Evaluate(stmt.Condition, scope) is not BoolValue b)
                throw new GeneratorException(
                    $"La condition du while doit être booléenne (ligne {stmt.SourceToken.Line})");
            if (!b.Content) break;

            if (++count > maxIterations)
                throw new GeneratorException(
                    $"Boucle while dépassant {maxIterations} itérations (ligne {stmt.SourceToken.Line})");

            var loopScope = new Scope(scope);
            foreach (var s in stmt.Body)
                Execute(s, loopScope, ws);
        }
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

    private void ExecuteSheetBlock(SheetBlock block, Scope scope)
    {
        if (!_declaredSheets.Contains(block.Name))
            throw new GeneratorException(
                $"sheet : la feuille '{block.Name}' n'a pas été déclarée avec sheets.add() " +
                $"(ligne {block.SourceToken.Line})");

        if (!_workbook.TryGetWorksheet(block.Name, out var ws))
            throw new GeneratorException(
                $"sheet : feuille '{block.Name}' introuvable dans le classeur " +
                $"(ligne {block.SourceToken.Line})");

        var sheetScope = new Scope(scope);
        foreach (var inner in block.Statements)
            Execute(inner, sheetScope, ws);
    }

    // ── cell(address_expr, value_expr) ────────────────────────────────────────

    private void ExecuteCell(IXLWorksheet ws, CellStatement stmt, Scope scope)
    {
        if (Evaluate(stmt.Address, scope) is not StringValue addrStr)
            throw new GeneratorException(
                $"L'adresse de cellule doit être une chaîne (ligne {stmt.SourceToken.Line})");

        ws.Cell(addrStr.Content).Value = Evaluate(stmt.Value, scope) switch
        {
            StringValue  s => XLCellValue.FromObject(s.Content),
            IntegerValue i => XLCellValue.FromObject(i.Content),
            FloatValue   f => XLCellValue.FromObject(f.Content),
            BoolValue    b => XLCellValue.FromObject(b.Content),
            ArrayValue   _ => throw new GeneratorException(
                     $"Impossible d'écrire un tableau dans la cellule '{addrStr.Content}' " +
                     $"(ligne {stmt.SourceToken.Line})"),
            _ => throw new GeneratorException(
                     $"Type de valeur non supporté à la ligne {stmt.SourceToken.Line}")
        };
    }

    // ── Evaluation ────────────────────────────────────────────────────────────

    private Value Evaluate(Expression expr, Scope scope) => expr switch
    {
        LiteralExpr lit      => lit.Val,
        VariableExpr v       => scope.Get(v.Name, v.SourceToken),
        ArrayLiteralExpr arr => new ArrayValue(arr.Items.Select(item => Evaluate(item, scope)).ToList()),
        ArrayAccessExpr a    => EvaluateArrayAccess(a, scope),
        BinaryExpr bin       => EvaluateBinary(bin, scope),
        UnaryExpr u          => EvaluateUnary(u, scope),
        _ => throw new GeneratorException(
                 $"Expression non supportée à la ligne {expr.SourceToken.Line}")
    };

    private Value EvaluateArrayAccess(ArrayAccessExpr access, Scope scope)
    {
        if (scope.Get(access.Name, access.SourceToken) is not ArrayValue arr)
            throw new GeneratorException(
                $"'{access.Name}' n'est pas un tableau (ligne {access.SourceToken.Line})");

        if (Evaluate(access.Index, scope) is not IntegerValue idx)
            throw new GeneratorException(
                $"L'index du tableau doit être un entier (ligne {access.SourceToken.Line})");

        if (idx.Content < 0 || idx.Content >= arr.Items.Count)
            throw new GeneratorException(
                $"Index {idx.Content} hors limites pour '{access.Name}' " +
                $"(taille : {arr.Items.Count}, ligne {access.SourceToken.Line})");

        return arr.Items[(int)idx.Content];
    }

    private Value EvaluateBinary(BinaryExpr expr, Scope scope)
    {
        var left  = Evaluate(expr.Left,  scope);
        var right = Evaluate(expr.Right, scope);

        return expr.Op switch
        {
            BinaryOp.Add => (left, right) switch
            {
                (IntegerValue l, IntegerValue r) => new IntegerValue(l.Content + r.Content),
                (FloatValue   l, FloatValue   r) => new FloatValue(l.Content + r.Content),
                (IntegerValue l, FloatValue   r) => new FloatValue(l.Content + r.Content),
                (FloatValue   l, IntegerValue r) => new FloatValue(l.Content + r.Content),
                (StringValue  l, StringValue  r) => new StringValue(l.Content + r.Content),
                (StringValue  l, IntegerValue r) => new StringValue(l.Content + r.Content.ToString()),
                (StringValue  l, FloatValue   r) => new StringValue(l.Content + r.Content.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                _ => throw new GeneratorException(
                         $"Opérandes incompatibles pour '+' (ligne {expr.SourceToken.Line})")
            },
            BinaryOp.Sub => (left, right) switch
            {
                (IntegerValue l, IntegerValue r) => new IntegerValue(l.Content - r.Content),
                (FloatValue   l, FloatValue   r) => new FloatValue(l.Content - r.Content),
                (IntegerValue l, FloatValue   r) => new FloatValue(l.Content - r.Content),
                (FloatValue   l, IntegerValue r) => new FloatValue(l.Content - r.Content),
                _ => throw new GeneratorException(
                         $"Opérandes incompatibles pour '-' (ligne {expr.SourceToken.Line})")
            },
            BinaryOp.Mul => (left, right) switch
            {
                (IntegerValue l, IntegerValue r) => new IntegerValue(l.Content * r.Content),
                (FloatValue   l, FloatValue   r) => new FloatValue(l.Content * r.Content),
                (IntegerValue l, FloatValue   r) => new FloatValue(l.Content * r.Content),
                (FloatValue   l, IntegerValue r) => new FloatValue(l.Content * r.Content),
                _ => throw new GeneratorException(
                         $"Opérandes incompatibles pour '*' (ligne {expr.SourceToken.Line})")
            },
            BinaryOp.Div => (left, right) switch
            {
                (IntegerValue l, IntegerValue r) => r.Content == 0
                    ? throw new GeneratorException($"Division par zéro (ligne {expr.SourceToken.Line})")
                    : new IntegerValue(l.Content / r.Content),
                (FloatValue   l, FloatValue   r) => new FloatValue(l.Content / r.Content),
                (IntegerValue l, FloatValue   r) => new FloatValue(l.Content / r.Content),
                (FloatValue   l, IntegerValue r) => new FloatValue(l.Content / r.Content),
                _ => throw new GeneratorException(
                         $"Opérandes incompatibles pour '/' (ligne {expr.SourceToken.Line})")
            },
            BinaryOp.Lt   => CompareNumeric(left, right, expr, (a, b) => a <  b),
            BinaryOp.Gt   => CompareNumeric(left, right, expr, (a, b) => a >  b),
            BinaryOp.LtEq => CompareNumeric(left, right, expr, (a, b) => a <= b),
            BinaryOp.GtEq => CompareNumeric(left, right, expr, (a, b) => a >= b),
            BinaryOp.EqEq  => new BoolValue(ValuesEqual(left, right)),
            BinaryOp.NotEq => new BoolValue(!ValuesEqual(left, right)),
            _ => throw new GeneratorException(
                     $"Opérateur non supporté (ligne {expr.SourceToken.Line})")
        };
    }

    private static BoolValue CompareNumeric(Value left, Value right, BinaryExpr expr,
        Func<double, double, bool> op)
    {
        double l = left switch
        {
            IntegerValue i => i.Content,
            FloatValue   f => f.Content,
            _ => throw new GeneratorException(
                     $"Comparaison numérique impossible sur ce type " +
                     $"(ligne {expr.SourceToken.Line})")
        };
        double r = right switch
        {
            IntegerValue i => i.Content,
            FloatValue   f => f.Content,
            _ => throw new GeneratorException(
                     $"Comparaison numérique impossible sur ce type " +
                     $"(ligne {expr.SourceToken.Line})")
        };
        return new BoolValue(op(l, r));
    }

    private static bool ValuesEqual(Value left, Value right) => (left, right) switch
    {
        (IntegerValue l, IntegerValue r) => l.Content == r.Content,
        (FloatValue   l, FloatValue   r) => l.Content == r.Content,
        (IntegerValue l, FloatValue   r) => l.Content == r.Content,
        (FloatValue   l, IntegerValue r) => l.Content == r.Content,
        (StringValue  l, StringValue  r) => l.Content == r.Content,
        (BoolValue    l, BoolValue    r) => l.Content == r.Content,
        _ => false
    };

    private Value EvaluateUnary(UnaryExpr expr, Scope scope)
    {
        var operand = Evaluate(expr.Operand, scope);
        return expr.Op switch
        {
            UnaryOp.Neg => operand switch
            {
                IntegerValue i => new IntegerValue(-i.Content),
                FloatValue   f => new FloatValue(-f.Content),
                _ => throw new GeneratorException(
                         $"Négation impossible sur ce type " +
                         $"(ligne {expr.SourceToken.Line})")
            },
            _ => throw new GeneratorException(
                     $"Opérateur unaire non supporté (ligne {expr.SourceToken.Line})")
        };
    }
}
