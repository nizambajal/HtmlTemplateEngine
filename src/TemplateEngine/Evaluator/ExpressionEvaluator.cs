using System.Collections;
using TemplateEngine.Ast;
using TemplateEngine.Context;
using TemplateEngine.Exceptions;

namespace TemplateEngine.Evaluator;

/// <summary>
/// Walks an <see cref="ExpressionAst"/> tree and produces a runtime value.
/// Stateless between calls; the context stack is passed per evaluation.
/// </summary>
public sealed class ExpressionEvaluator
{
    private readonly PropertyResolver _resolver;
    private readonly Dictionary<string, Func<object?, object?>> _helpers;

    public ExpressionEvaluator(PropertyResolver resolver, Dictionary<string, Func<object?, object?>> helpers)
    {
        _resolver = resolver;
        _helpers = helpers;
    }

    /// <summary>Evaluates an expression AST node against the current context stack.</summary>
    public object? Evaluate(ExpressionAst expr, ContextStack ctx)
    {
        return expr switch
        {
            StringLiteralExpr s => s.Value,
            NumberLiteralExpr n => n.Value,
            BoolLiteralExpr b => b.Value,
            NullLiteralExpr => null,
            PropertyExpr p => EvalProperty(p, ctx),
            LoopMetaExpr m => EvalLoopMeta(m, ctx),
            BinaryExpr bin => EvalBinary(bin, ctx),
            UnaryExpr un => EvalUnary(un, ctx),
            HelperCallExpr h => EvalHelper(h, ctx),
            _ => throw new RenderingException($"Unknown expression type {expr.GetType().Name}", expr.Line, expr.Column)
        };
    }

    // ── Property resolution ───────────────────────────────────────────────────

    private object? EvalProperty(PropertyExpr expr, ContextStack ctx)
    {
        var path = expr.Path;

        // Strip and count parent-navigation prefixes (../)
        int parentLevels = 0;
        while (path.StartsWith("../", StringComparison.Ordinal))
        {
            parentLevels++;
            path = path[3..];
        }

        // Explicit parent navigation — resolve against a specific ancestor frame
        if (parentLevels > 0)
        {
            var frame = ctx.FrameAt(parentLevels);
            if (frame == null)
                throw new PropertyResolutionException(expr.Path, expr.Line, expr.Column,
                    $"Parent context level {parentLevels} does not exist (stack depth {ctx.Depth})");
            return _resolver.Resolve(frame.Model, path, expr.Line, expr.Column);
        }

        // Normal resolution: walk frames from innermost outward.
        // *** Hot path — zero exceptions thrown here ***
        foreach (var f in ctx.Frames)
        {
            if (f.Alias != null)
            {
                // This frame was created by a foreach-as; only match if path starts with alias
                if (!path.StartsWith(f.Alias, StringComparison.OrdinalIgnoreCase))
                    continue;

                // "item" (exact alias) → return the model itself
                if (path.Length == f.Alias.Length)
                    return f.Model;

                // "item.Name" → resolve "Name" against the item model
                if (path.Length > f.Alias.Length && path[f.Alias.Length] == '.')
                {
                    var subPath = path[(f.Alias.Length + 1)..];
                    if (_resolver.TryResolve(f.Model, subPath, out var aliasVal))
                        return aliasVal;
                }
                // Alias matched prefix but sub-path not found — don't fall through
                // to parent frames (wrong scope), report the error now.
                throw new PropertyResolutionException(expr.Path, expr.Line, expr.Column,
                    $"Could not resolve '{path[(f.Alias.Length + 1)..]}' on alias '{f.Alias}'");
            }
            else
            {
                // Non-aliased frame — try to resolve the full path
                if (_resolver.TryResolve(f.Model, path, out var val))
                    return val;
                // Miss on this frame → try parent frame
            }
        }

        throw new PropertyResolutionException(expr.Path, expr.Line, expr.Column,
            "Could not resolve in any context frame");
    }

    // ── Loop metadata ─────────────────────────────────────────────────────────

    private static object? EvalLoopMeta(LoopMetaExpr expr, ContextStack ctx)
    {
        foreach (var frame in ctx.Frames)
        {
            if (frame.LoopMeta == null) continue;
            return expr.MetaName switch
            {
                "index" => (object)frame.LoopMeta.Index,
                "first" => frame.LoopMeta.IsFirst,
                "last" => frame.LoopMeta.IsLast,
                "count" => frame.LoopMeta.Count,
                _ => null
            };
        }
        return null;
    }

    // ── Binary expressions ────────────────────────────────────────────────────

    private object? EvalBinary(BinaryExpr expr, ContextStack ctx)
    {
        // Short-circuit logical operators — evaluate right only when needed
        if (expr.Operator == "&&")
        {
            var l = Evaluate(expr.Left, ctx);
            if (!IsTruthy(l)) return false;
            return IsTruthy(Evaluate(expr.Right, ctx));
        }
        if (expr.Operator == "||")
        {
            var l = Evaluate(expr.Left, ctx);
            if (IsTruthy(l)) return true;
            return IsTruthy(Evaluate(expr.Right, ctx));
        }

        var left = Evaluate(expr.Left, ctx);
        var right = Evaluate(expr.Right, ctx);

        return expr.Operator switch
        {
            "==" => AreEqual(left, right),
            "!=" => !AreEqual(left, right),
            ">" => Compare(left, right) > 0,
            "<" => Compare(left, right) < 0,
            ">=" => Compare(left, right) >= 0,
            "<=" => Compare(left, right) <= 0,
            "+" => Add(left, right),
            "-" => Arithmetic(left, right, (a, b) => a - b),
            "*" => Arithmetic(left, right, (a, b) => a * b),
            "/" => Arithmetic(left, right, (a, b) => a / b),
            "%" => Arithmetic(left, right, (a, b) => a % b),
            _ => throw new RenderingException($"Unknown operator '{expr.Operator}'", expr.Line, expr.Column)
        };
    }

    private object? EvalUnary(UnaryExpr expr, ContextStack ctx)
    {
        var val = Evaluate(expr.Operand, ctx);
        return expr.Operator switch
        {
            "!" => !IsTruthy(val),
            "-" => val is double d ? -d : val is int i ? (object)-i
                   : throw new RenderingException("Cannot negate non-numeric value", expr.Line, expr.Column),
            _ => throw new RenderingException($"Unknown unary operator '{expr.Operator}'", expr.Line, expr.Column)
        };
    }

    private object? EvalHelper(HelperCallExpr expr, ContextStack ctx)
    {
        if (!_helpers.TryGetValue(expr.HelperName, out var helper))
            throw new RenderingException($"Helper '{expr.HelperName}' is not registered",
                expr.Line, expr.Column, expr.HelperName);

        var arg = expr.Arguments.Count > 0 ? Evaluate(expr.Arguments[0], ctx) : null;
        return helper(arg);
    }

    // ── Value utilities ───────────────────────────────────────────────────────

    public static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        int i => i != 0,
        long l => l != 0,
        double d => d != 0.0,
        string s => s.Length > 0,
        ICollection c => c.Count > 0,
        IEnumerable e => e.Cast<object>().Any(),
        _ => true
    };

    private static bool AreEqual(object? a, object? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (TryDouble(a, out var da) && TryDouble(b, out var db))
            return Math.Abs(da - db) < 1e-10;
        return a.Equals(b) || a.ToString() == b.ToString();
    }

    private static int Compare(object? a, object? b)
    {
        if (a is null && b is null) return 0;
        if (a is null) return -1;
        if (b is null) return 1;
        if (TryDouble(a, out var da) && TryDouble(b, out var db))
            return da.CompareTo(db);
        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    private static object? Add(object? a, object? b)
    {
        if (a is string || b is string) return $"{a}{b}";
        if (TryDouble(a, out var da) && TryDouble(b, out var db)) return da + db;
        return $"{a}{b}";
    }

    private static object? Arithmetic(object? a, object? b, Func<double, double, double> op)
    {
        if (TryDouble(a, out var da) && TryDouble(b, out var db)) return op(da, db);
        throw new InvalidOperationException($"Cannot perform arithmetic on '{a}' and '{b}'");
    }

    private static bool TryDouble(object? v, out double d)
    {
        d = 0;
        if (v is null) return false;
        try { d = Convert.ToDouble(v); return true; } catch { return false; }
    }
}