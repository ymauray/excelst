namespace Excelst.Compiler;

// ── Values ────────────────────────────────────────────────────────────────────

public abstract record Value;
public sealed record StringValue(string Content) : Value;
public sealed record IntegerValue(long Content) : Value;
public sealed record FloatValue(double Content) : Value;
public sealed record VariableValue(string Name) : Value;
public sealed record ArrayValue(IReadOnlyList<Value> Items) : Value;
public sealed record ArrayAccessValue(string Name, int Index) : Value;

// ── Inner statements (inside a sheet block) ───────────────────────────────────

public abstract record InnerStatement(Token SourceToken);

public sealed record CellStatement(
    string Address,
    Value Value,
    Token SourceToken
) : InnerStatement(SourceToken);

// ── Top-level statements ──────────────────────────────────────────────────────

public abstract record TopLevelStatement(Token SourceToken);

/// <summary>let name = value — déclare une variable globale.</summary>
public sealed record LetStatement(
    string Name,
    Value Value,
    Token SourceToken
) : TopLevelStatement(SourceToken);

/// <summary>sheets.add("name", after: "other") — déclare une feuille et son ordre.</summary>
public sealed record SheetsAddStatement(
    string Name,
    string? After,
    Token SourceToken
) : TopLevelStatement(SourceToken);

/// <summary>sheet("name", { ... }) — définit le contenu d'une feuille déjà déclarée.</summary>
public sealed record SheetBlock(
    string Name,
    IReadOnlyList<InnerStatement> Statements,
    Token SourceToken
) : TopLevelStatement(SourceToken);

// ── Programme ─────────────────────────────────────────────────────────────────

public sealed record Programme(IReadOnlyList<TopLevelStatement> Statements);
