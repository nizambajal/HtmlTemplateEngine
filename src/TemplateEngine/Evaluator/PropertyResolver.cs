using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using TemplateEngine.Exceptions;

namespace TemplateEngine.Evaluator;

/// <summary>
/// Resolves a dotted property path against an object model.
/// Uses compiled delegates cached per (Type, PropertyName) and cached segment
/// lists per path string — zero repeated reflection or string splitting on
/// the hot render path.
/// </summary>
public sealed class PropertyResolver
{
    // (Type, MemberName) -> getter delegate; null means member not found
    private static readonly ConcurrentDictionary<(Type, string), Func<object, object?>?> _getterCache = new();

    // Path string -> pre-split segment list; avoids re-splitting on every render
    private static readonly ConcurrentDictionary<string, (string Segment, bool NullSafe)[]> _segmentCache = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves <paramref name="path"/> against <paramref name="model"/>, throwing on failure.
    /// </summary>
    public object? Resolve(object? model, string path, int line = 0, int col = 0)
    {
        if (TryResolve(model, path, out var value))
            return value;
        throw new PropertyResolutionException(path, line, col,
            $"Could not resolve '{path}' on type '{model?.GetType().Name ?? "null"}'");
    }

    /// <summary>
    /// Attempts to resolve <paramref name="path"/> against <paramref name="model"/>.
    /// Returns <c>false</c> — never throws — when the property does not exist.
    /// This is the zero-exception hot path used by the frame-walking loop.
    /// </summary>
    public bool TryResolve(object? model, string path, out object? value)
    {
        value = null;
        if (model == null) return true;          // null model → null value (not an error)
        if (string.IsNullOrWhiteSpace(path)) { value = model; return true; }

        var segments = GetSegments(path);
        object? current = model;

        foreach (var (segment, nullSafe) in segments)
        {
            if (current == null)
            {
                if (nullSafe) { value = null; return true; }
                return false;                     // non-null-safe path hit null — miss
            }

            if (!TryResolveSegment(current, segment, out current))
                return false;
        }

        value = current;
        return true;
    }

    // ── Segment resolution ────────────────────────────────────────────────────

    private bool TryResolveSegment(object obj, string segment, out object? result)
    {
        result = null;
        var type = obj.GetType();

        // Special members: Count / Length
        if (segment.Equals("Count", StringComparison.OrdinalIgnoreCase))
        {
            if (obj is string s) { result = s.Length; return true; }
            if (obj is ICollection col) { result = col.Count; return true; }
            //if (obj is IEnumerable en) { result = en.Cast<object>().Count(); return true; }
            if (obj is IReadOnlyCollection<object> roc)
            {
                result = roc.Count;
                return true;
            }

            if (obj is IEnumerable en)
            {
                var list = en as IList ?? en.Cast<object>().ToList();

                result = list.Count;
                return true;
            }
        }
        if (segment.Equals("Length", StringComparison.OrdinalIgnoreCase))
        {
            if (obj is string s2) { result = s2.Length; return true; }
            if (obj is Array arr) { result = arr.Length; return true; }
        }

        // Dictionary indexer
        if (obj is IDictionary dict && dict.Contains(segment))
        {
            result = dict[segment];
            return true;
        }

        // Cached property/field getter
        var getter = _getterCache.GetOrAdd((type, segment), static key => BuildGetter(key.Item1, key.Item2));
        if (getter == null) return false;

        result = getter(obj);
        return true;
    }

    //private static Func<object, object?>? BuildGetter(Type type, string name)
    //{
    //    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    //    if (prop != null)
    //    {
    //        var mi = prop.GetGetMethod()!;
    //        return obj => mi.Invoke(obj, null);
    //    }

    //    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    //    if (field != null) return field.GetValue;

    //    return null;
    //}

    // Perofrmance enhanced
    private static Func<object, object?>? BuildGetter(Type type, string name)
    {
        var prop = type.GetProperty(
            name,
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.IgnoreCase);

        if (prop != null)
        {
            var objParam = Expression.Parameter(typeof(object), "obj");

            var castObj = Expression.Convert(objParam, type);

            var property = Expression.Property(castObj, prop);

            var convertResult = Expression.Convert(
                property,
                typeof(object));

            return Expression
                .Lambda<Func<object, object?>>(
                    convertResult,
                    objParam)
                .Compile();
        }

        var field = type.GetField(
            name,
            BindingFlags.Public |
            BindingFlags.Instance |
            BindingFlags.IgnoreCase);

        if (field != null)
        {
            var objParam = Expression.Parameter(typeof(object));

            var castObj = Expression.Convert(objParam, type);

            var fieldExpr = Expression.Field(castObj, field);

            var convertResult = Expression.Convert(
                fieldExpr,
                typeof(object));

            return Expression
                .Lambda<Func<object, object?>>(
                    convertResult,
                    objParam)
                .Compile();
        }

        return null;
    }

    // ── Segment caching ───────────────────────────────────────────────────────

    private static (string Segment, bool NullSafe)[] GetSegments(string path)
        => _segmentCache.GetOrAdd(path, static p => ParseSegments(p));

    private static (string Segment, bool NullSafe)[] ParseSegments(string path)
    {
        var parts = path.Split('.');
        var result = new List<(string, bool)>(parts.Length);

        foreach (var part in parts)
        {
            if (part.EndsWith('?'))
            {
                result.Add((part[..^1], true));
            }
            else if (part.Contains("?."))
            {
                var sub = part.Split("?.");
                result.Add((sub[0], false));
                for (int i = 1; i < sub.Length; i++)
                    result.Add((sub[i], true));
            }
            else
            {
                result.Add((part, false));
            }
        }

        return result.ToArray();   // array is faster to iterate than List<>
    }
}