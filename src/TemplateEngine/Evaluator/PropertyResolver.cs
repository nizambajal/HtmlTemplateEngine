using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using TemplateEngine.Exceptions;

namespace TemplateEngine.Evaluator;

/// <summary>
/// Resolves a dotted property path against an object model.
/// Uses compiled delegates cached per type+property to avoid repeated reflection.
/// </summary>
public sealed class PropertyResolver
{
    // Cache: (Type, PropertyName) -> compiled getter
    private static readonly ConcurrentDictionary<(Type, string), Func<object, object?>?> _getterCache = new();

    /// <summary>
    /// Resolves a dotted property path (e.g. "Customer.Address.City") against <paramref name="model"/>.
    /// Supports:
    /// <list type="bullet">
    ///   <item>Standard properties and fields</item>
    ///   <item>Dictionary&lt;string,?&gt; indexer access</item>
    ///   <item>IEnumerable.Count (resolved via Count/Length property)</item>
    ///   <item>Null-safe segments (?.)</item>
    /// </list>
    /// </summary>
    public object? Resolve(object? model, string path, int line = 0, int col = 0)
    {
        if (model == null) return null;
        if (string.IsNullOrWhiteSpace(path)) return model;

        // Split by '.' and '?.' while retaining null-safe info per segment
        var segments = SplitPath(path);
        object? current = model;

        foreach (var (segment, nullSafe) in segments)
        {
            if (current == null)
            {
                if (nullSafe) return null;
                throw new PropertyResolutionException(path, line, col, "Null reference encountered while navigating path");
            }

            current = ResolveSegment(current, segment, path, line, col);
        }

        return current;
    }

    private object? ResolveSegment(object obj, string segment, string fullPath, int line, int col)
    {
        var type = obj.GetType();

        // Special member: Count / Length on collections / strings
        if (segment.Equals("Count", StringComparison.OrdinalIgnoreCase))
        {
            if (obj is string s) return s.Length;
            if (obj is ICollection coll) return coll.Count;
            // Try IEnumerable<T>.Count()
            if (obj is IEnumerable enumerable) return enumerable.Cast<object>().Count();
        }

        if (segment.Equals("Length", StringComparison.OrdinalIgnoreCase))
        {
            if (obj is string s) return s.Length;
            if (obj is Array arr) return arr.Length;
        }

        // Dictionary access
        if (obj is IDictionary dict)
        {
            if (dict.Contains(segment)) return dict[segment];
        }

        // Cached getter
        var getter = _getterCache.GetOrAdd((type, segment), key => BuildGetter(key.Item1, key.Item2));
        if (getter != null) return getter(obj);

        throw new PropertyResolutionException(fullPath, line, col,
            $"Property or field '{segment}' not found on type '{type.Name}'");
    }

    private static Func<object, object?>? BuildGetter(Type type, string name)
    {
        // Try property
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop != null)
        {
            var getter = prop.GetGetMethod()!;
            return obj => getter.Invoke(obj, null);
        }

        // Try field
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (field != null) return obj => field.GetValue(obj);

        return null;
    }

    private static List<(string Segment, bool NullSafe)> SplitPath(string path)
    {
        var result = new List<(string, bool)>();
        var parts = path.Split('.');

        foreach (var part in parts)
        {
            if (part.EndsWith('?'))
            {
                result.Add((part[..^1], true));
            }
            else if (part.Contains("?."))
            {
                // e.g. "Customer?.Name" already processed by lexer into two parts
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
        return result;
    }
}
