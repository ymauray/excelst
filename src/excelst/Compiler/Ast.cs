namespace Excelst.Compiler;

// ── Runtime values (sortie du générateur) ─────────────────────────────────────

public abstract record Value;
public sealed record StringValue(string Content) : Value;
public sealed record IntegerValue(long Content) : Value;
public sealed record FloatValue(double Content) : Value;
public sealed record BoolValue(bool Content) : Value;
public sealed record ArrayValue(IReadOnlyList<Value> Items) : Value;

// ── Opérateurs ────────────────────────────────────────────────────────────────

public enum BinaryOp { Add, Sub, Mul, Div, Lt, Gt, LtEq, GtEq, EqEq, NotEq }
public enum UnaryOp  { Neg }

// ── Expressions (nœuds AST — sortie du parser) ────────────────────────────────

public abstract record Expression(Token SourceToken);

public sealed record LiteralExpr(Value Val, Token SourceToken)
    : Expression(SourceToken);

public sealed record VariableExpr(string Name, Token SourceToken)
    : Expression(SourceToken);

public sealed record ArrayLiteralExpr(
    IReadOnlyList<Expression> Items,
    Token SourceToken
) : Expression(SourceToken);

public sealed record ArrayAccessExpr(
    string Name,
    Expression Index,
    Token SourceToken
) : Expression(SourceToken);

public sealed record BinaryExpr(
    Expression Left,
    BinaryOp Op,
    Expression Right,
    Token SourceToken
) : Expression(SourceToken);

public sealed record UnaryExpr(
    UnaryOp Op,
    Expression Operand,
    Token SourceToken
) : Expression(SourceToken);

// ── Instructions (nœuds AST — hiérarchie unifiée) ────────────────────────────

public abstract record Statement(Token SourceToken);

/// <summary>let name = expr — liaison immutable.</summary>
public sealed record LetStatement(
    string Name, Expression Initializer, Token SourceToken
) : Statement(SourceToken);

/// <summary>var name = expr — liaison mutable.</summary>
public sealed record VarStatement(
    string Name, Expression Initializer, Token SourceToken
) : Statement(SourceToken);

/// <summary>name = expr — réassignation d'une variable mutable.</summary>
public sealed record AssignStatement(
    string Name, Expression Value, Token SourceToken
) : Statement(SourceToken);

/// <summary>while condition { body }</summary>
public sealed record WhileStatement(
    Expression Condition,
    IReadOnlyList<Statement> Body,
    Token SourceToken
) : Statement(SourceToken);

/// <summary>sheets.add("name", after: "other")</summary>
public sealed record SheetsAddStatement(
    string Name, string? After, Token SourceToken
) : Statement(SourceToken);

/// <summary>sheet("name", { ... })</summary>
public sealed record SheetBlock(
    string Name,
    IReadOnlyList<Statement> Statements,
    Token SourceToken
) : Statement(SourceToken);

/// <summary>cell(address_expr, value_expr)</summary>
public sealed record CellStatement(
    Expression Address,
    Expression Value,
    Token SourceToken
) : Statement(SourceToken);

// ── Programme ─────────────────────────────────────────────────────────────────

public sealed record Programme(IReadOnlyList<Statement> Statements);
