using System.Collections;
using System.Text;
using System.Web;
using TemplateEngine.Ast;
using TemplateEngine.Context;
using TemplateEngine.Evaluator;
using TemplateEngine.Exceptions;
using TemplateEngine.Formatting;

namespace TemplateEngine.Rendering;

/// <summary>
/// Walks the AST and emits the final rendered string.
/// A new instance should be created per render call (not shared).
/// </summary>
public sealed class Renderer
{
    private readonly ExpressionEvaluator _evaluator;
    private readonly FormatterEngine _formatter;
    private readonly Dictionary<string, Func<string, ContextStack, string>> _blockHelpers;

    public Renderer(
        ExpressionEvaluator evaluator,
        FormatterEngine formatter,
        Dictionary<string, Func<string, ContextStack, string>> blockHelpers)
    {
        _evaluator = evaluator;
        _formatter = formatter;
        _blockHelpers = blockHelpers;
    }

    /// <summary>Renders the document AST against the supplied context stack.</summary>
    public string Render(DocumentNode doc, ContextStack ctx)
    {
        var sb = new StringBuilder(4096);
        RenderChildren(doc.Children, ctx, sb);
        return sb.ToString();
    }

    private void RenderChildren(IEnumerable<AstNode> nodes, ContextStack ctx, StringBuilder sb)
    {
        foreach (var node in nodes)
            RenderNode(node, ctx, sb);
    }

    private void RenderNode(AstNode node, ContextStack ctx, StringBuilder sb)
    {
        switch (node)
        {
            case TextNode text:
                sb.Append(text.Content);
                break;

            case ExpressionNode expr:
                RenderExpression(expr, ctx, sb);
                break;

            case IfNode ifNode:
                RenderIf(ifNode, ctx, sb);
                break;

            case ForeachNode foreachNode:
                RenderForeach(foreachNode, ctx, sb);
                break;

            default:
                throw new RenderingException($"Unknown AST node type: {node.GetType().Name}", node.Line, node.Column);
        }
    }

    // ── Expression rendering ──────────────────────────────────────────────────

    private void RenderExpression(ExpressionNode node, ContextStack ctx, StringBuilder sb)
    {
        object? value;
        try
        {
            value = _evaluator.Evaluate(node.Expression, ctx);
        }
        catch (TemplateEngineException) { throw; }
        catch (Exception ex)
        {
            throw new RenderingException($"Error evaluating expression", node.Line, node.Column, inner: ex);
        }

        string text;
        if (node.Format != null)
        {
            text = _formatter.Format(value, node.Format);
        }
        else
        {
            text = value?.ToString() ?? string.Empty;
        }

        // HTML encode unless raw
        if (node.IsRaw)
            sb.Append(text);
        else
            sb.Append(HttpUtility.HtmlEncode(text));
    }

    // ── If rendering ──────────────────────────────────────────────────────────

    private void RenderIf(IfNode node, ContextStack ctx, StringBuilder sb)
    {
        if (EvalCondition(node.Condition, ctx, node.Line, node.Column))
        {
            RenderChildren(node.ThenBody, ctx, sb);
            return;
        }

        foreach (var elseIf in node.ElseIfBranches)
        {
            if (EvalCondition(elseIf.Condition, ctx, node.Line, node.Column))
            {
                RenderChildren(elseIf.Body, ctx, sb);
                return;
            }
        }

        if (node.ElseBody != null)
            RenderChildren(node.ElseBody, ctx, sb);
    }

    private bool EvalCondition(ExpressionAst expr, ContextStack ctx, int line, int col)
    {
        try
        {
            var val = _evaluator.Evaluate(expr, ctx);
            return ExpressionEvaluator.IsTruthy(val);
        }
        catch (TemplateEngineException) { throw; }
        catch (Exception ex)
        {
            throw new RenderingException("Error evaluating condition", line, col, inner: ex);
        }
    }

    // ── Foreach rendering ─────────────────────────────────────────────────────

    private void RenderForeach(ForeachNode node, ContextStack ctx, StringBuilder sb)
    {
        // Resolve the collection from the path
        object? collection;
        try
        {
            collection = ResolveCollection(node.Collection, ctx, node.Line, node.Column);
        }
        catch (TemplateEngineException) { throw; }
        catch (Exception ex)
        {
            throw new RenderingException($"Cannot resolve collection '{node.Collection}'", node.Line, node.Column, inner: ex);
        }

        if (collection == null) return;

        var items = ToObjectList(collection);
        int count = items.Count;

        for (int i = 0; i < count; i++)
        {
            var loopMeta = new LoopContext { Index = i, Count = count };
            ctx.Push(items[i], node.Alias, loopMeta);
            try
            {
                RenderChildren(node.Body, ctx, sb);
            }
            finally
            {
                ctx.Pop();
            }
        }
    }

    private object? ResolveCollection(string path, ContextStack ctx, int line, int col)
    {
        // Build a temporary property expression and evaluate it
        var expr = new PropertyExpr { Path = path, Line = line, Column = col };
        return _evaluator.Evaluate(expr, ctx);
    }

    private static List<object?> ToObjectList(object collection)
    {
        if (collection is IEnumerable enumerable)
            return enumerable.Cast<object?>().ToList();
        return new List<object?> { collection };
    }
}
