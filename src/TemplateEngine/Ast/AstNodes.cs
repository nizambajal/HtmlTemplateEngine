namespace TemplateEngine.Ast;

/// <summary>Base class for all AST nodes.</summary>
public abstract class AstNode
{
    public int Line { get; init; }
    public int Column { get; init; }
}

/// <summary>Root node holding all top-level children.</summary>
public sealed class DocumentNode : AstNode
{
    public List<AstNode> Children { get; } = new();
}

/// <summary>Raw text/HTML content node.</summary>
public sealed class TextNode : AstNode
{
    public string Content { get; init; } = string.Empty;
}

/// <summary>
/// An interpolated expression: {{expr}} or {{expr:format}}.
/// </summary>
public sealed class ExpressionNode : AstNode
{
    public ExpressionAst Expression { get; init; } = null!;
    public string? Format { get; init; }
    public bool IsRaw { get; init; }
}

/// <summary>{{#if ...}} block with optional else/else-if chains.</summary>
public sealed class IfNode : AstNode
{
    public ExpressionAst Condition { get; init; } = null!;
    public List<AstNode> ThenBody { get; } = new();
    public List<ElseIfBranch> ElseIfBranches { get; } = new();
    public List<AstNode>? ElseBody { get; set; }
}

/// <summary>A single {{else if Cond}} branch.</summary>
public sealed class ElseIfBranch
{
    public ExpressionAst Condition { get; init; } = null!;
    public List<AstNode> Body { get; } = new();
}

/// <summary>{{#foreach Collection as alias}} loop.</summary>
public sealed class ForeachNode : AstNode
{
    public string Collection { get; init; } = string.Empty;
    public string? Alias { get; init; }
    public List<AstNode> Body { get; } = new();
}

// ── Expression AST ────────────────────────────────────────────────────────────

/// <summary>Base for all expression sub-trees.</summary>
public abstract class ExpressionAst
{
    public int Line { get; init; }
    public int Column { get; init; }
}

/// <summary>A dotted property path, possibly with null-safe segments and parent navigation.</summary>
public sealed class PropertyExpr : ExpressionAst
{
    /// <summary>
    /// Full path string, e.g. "Customer.Name", "../OrderNumber", "../../Customer.Name",
    /// "Customer?.Address?.City".
    /// </summary>
    public string Path { get; init; } = string.Empty;
}

/// <summary>A string literal.</summary>
public sealed class StringLiteralExpr : ExpressionAst
{
    public string Value { get; init; } = string.Empty;
}

/// <summary>A numeric literal.</summary>
public sealed class NumberLiteralExpr : ExpressionAst
{
    public double Value { get; init; }
}

/// <summary>A boolean literal.</summary>
public sealed class BoolLiteralExpr : ExpressionAst
{
    public bool Value { get; init; }
}

/// <summary>The null literal.</summary>
public sealed class NullLiteralExpr : ExpressionAst { }

/// <summary>Binary operation, e.g. a + b, a == b, a &amp;&amp; b.</summary>
public sealed class BinaryExpr : ExpressionAst
{
    public string Operator { get; init; } = string.Empty;
    public ExpressionAst Left { get; init; } = null!;
    public ExpressionAst Right { get; init; } = null!;
}

/// <summary>Unary operation, e.g. !condition.</summary>
public sealed class UnaryExpr : ExpressionAst
{
    public string Operator { get; init; } = string.Empty;
    public ExpressionAst Operand { get; init; } = null!;
}

/// <summary>A helper function call: Upper(Name).</summary>
public sealed class HelperCallExpr : ExpressionAst
{
    public string HelperName { get; init; } = string.Empty;
    public List<ExpressionAst> Arguments { get; } = new();
}

/// <summary>A loop metadata variable: @index, @first, @last, @count.</summary>
public sealed class LoopMetaExpr : ExpressionAst
{
    public string MetaName { get; init; } = string.Empty;
}
