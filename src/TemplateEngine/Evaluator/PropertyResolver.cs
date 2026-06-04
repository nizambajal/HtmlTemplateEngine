using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Dynamic;
using TemplateEngine.Exceptions;

namespace TemplateEngine.Evaluator;

/// <summary>
/// Resolves a dotted property path against any model type.
///
/// Supported model types (tried in order):
/// <list type="number">
///   <item><see cref="JsonElement"/> — uses JsonElement.GetProperty / JsonElement.GetInt32 etc.</item>
///   <item><see cref="IDictionary{TKey,TValue}"/> / <see cref="IDictionary"/> — key lookup</item>
///   <item><see cref="ICollection"/> / <see cref="IEnumerable"/> — Count / Length</item>
///   <item>Any POCO / anonymous type — reflection with compiled delegate cache</item>
/// </list>
///
/// All property getters are cached per (Type, MemberName) so reflection only
/// runs once per unique type+property combination.
/// </summary>
public sealed class PropertyResolver
{
    // Compiled getter cache: (Type, MemberName) → delegate | null-if-not-found
    private static readonly ConcurrentDictionary<(Type, string), Func<object, object?>?> _getterCache = new();

    // Segment cache: path string → pre-split array, avoids re-splitting on every render
    private static readonly ConcurrentDictionary<string, (string Segment, bool NullSafe)[]> _segmentCache = new();

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves <paramref name="path"/> against <paramref name="model"/>, throwing
    /// <see cref="PropertyResolutionException"/> on failure.
    /// </summary>
    public object? Resolve(object? model, string path, int line = 0, int col = 0)
    {
        if (!TryResolve(model, path, out var value))
            throw new PropertyResolutionException(path, line, col,
                $"Could not resolve '{path}' on type '{model?.GetType().Name ?? "null"}'");
        return value;
    }

    /// <summary>
    /// Attempts to resolve <paramref name="path"/> against <paramref name="model"/>.
    /// Returns <c>false</c> — never throws — when the member does not exist.
    /// This is the zero-exception hot path used by the frame-walking loop.
    /// </summary>
    public bool TryResolve(object? model, string path, out object? value)
    {
        value = null;
        if (model == null) return true;
        if (string.IsNullOrWhiteSpace(path)) { value = model; return true; }

        var segments = GetSegments(path);
        object? current = model;

        foreach (var (segment, nullSafe) in segments)
        {
            // Current object is null — only continue if THIS segment is null-safe
            if (current == null)
            {
                if (nullSafe) { value = null; return true; }
                return false;
            }

            // Resolve this segment; if missing and null-safe, return null gracefully.
            if (!TryResolveSegment(current, segment, out current))
            {
                if (nullSafe) { value = null; return true; }
                return false;
            }
        }

        value = current;
        return true;
    }

    // ── Per-segment resolution ─────────────────────────────────────────────────

    private bool TryResolveSegment(object obj, string segment, out object? result)
    {
        result = null;

        // ── JsonElement ───────────────────────────────────────────────────────
        if (obj is JsonElement je)
            return TryResolveJsonElement(je, segment, out result);

        // ── ExpandoObject / IDictionary<string,object?> ───────────────────────
        if (obj is IDictionary<string, object?> expando)
        {
            if (expando.TryGetValue(segment, out result)) return true;
            // case-insensitive fallback
            foreach (var kv in expando)
                if (string.Equals(kv.Key, segment, StringComparison.OrdinalIgnoreCase))
                { result = kv.Value; return true; }
            return false;
        }

        // ── Generic/non-generic dictionary ───────────────────────────────────
        if (obj is IDictionary dict)
        {
            if (dict.Contains(segment)) { result = dict[segment]; return true; }
            return false;
        }

        // ── Count / Length on collections ─────────────────────────────────────
        if (segment.Equals("Count", StringComparison.OrdinalIgnoreCase))
        {
            if (obj is string s) { result = s.Length; return true; }
            if (obj is ICollection col) { result = col.Count; return true; }
            if (obj is IEnumerable en) { result = en.Cast<object>().Count(); return true; }
        }
        if (segment.Equals("Length", StringComparison.OrdinalIgnoreCase))
        {
            if (obj is string s2) { result = s2.Length; return true; }
            if (obj is Array arr) { result = arr.Length; return true; }
        }

        // ── POCO / anonymous type via compiled delegate cache ─────────────────
        var type = obj.GetType();
        var getter = _getterCache.GetOrAdd((type, segment), static k => BuildGetter(k.Item1, k.Item2));
        if (getter == null) return false;

        result = getter(obj);
        return true;
    }

    // ── JsonElement resolution ─────────────────────────────────────────────────

    private static bool TryResolveJsonElement(JsonElement je, string segment, out object? result)
    {
        result = null;

        // Array length / object property count
        if (segment.Equals("Count", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("Length", StringComparison.OrdinalIgnoreCase))
        {
            if (je.ValueKind == JsonValueKind.Array)
            { result = je.GetArrayLength(); return true; }
            if (je.ValueKind == JsonValueKind.Object)
            { result = je.EnumerateObject().Count(); return true; }
        }

        // Numeric array index
        if (je.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var idx))
        {
            if (idx >= 0 && idx < je.GetArrayLength())
            { result = UnwrapJsonElement(je[idx]); return true; }
            return false;
        }

        // Object property lookup (case-insensitive)
        if (je.ValueKind == JsonValueKind.Object &&
            je.TryGetProperty(segment, out var child))
        {
            result = UnwrapJsonElement(child);
            return true;
        }

        // Case-insensitive fallback for object properties
        if (je.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in je.EnumerateObject())
            {
                if (string.Equals(prop.Name, segment, StringComparison.OrdinalIgnoreCase))
                { result = UnwrapJsonElement(prop.Value); return true; }
            }
        }

        return false;
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> leaf value to the closest .NET primitive
    /// so formatters and comparisons work naturally downstream.
    /// Non-leaf values (Object / Array) are returned as <see cref="JsonElement"/> so
    /// nested path segments can keep resolving into them.
    /// </summary>
    private static object? UnwrapJsonElement(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? (object)l
                               : el.TryGetDouble(out var d) ? d
                               : el.GetRawText(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el   // Object or Array — keep as JsonElement for further navigation
    };

    // ── Reflection getter builder ──────────────────────────────────────────────

    private static Func<object, object?>? BuildGetter(Type type, string name)
    {
        // Property — build a compiled expression tree delegate for near-native speed
        var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop != null)
        {
            try
            {
                var param = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
                var cast = System.Linq.Expressions.Expression.Convert(param, type);
                var access = System.Linq.Expressions.Expression.Property(cast, prop);
                var boxed = System.Linq.Expressions.Expression.Convert(access, typeof(object));
                return System.Linq.Expressions.Expression.Lambda<Func<object, object?>>(boxed, param).Compile();
            }
            catch
            {
                // Fallback to MethodInfo.Invoke if expression compilation fails (e.g. ref structs)
                var mi = prop.GetGetMethod()!;
                return obj => mi.Invoke(obj, null);
            }
        }

        // Field
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (field != null)
        {
            var param = System.Linq.Expressions.Expression.Parameter(typeof(object), "o");
            var cast = System.Linq.Expressions.Expression.Convert(param, type);
            var access = System.Linq.Expressions.Expression.Field(cast, field);
            var boxed = System.Linq.Expressions.Expression.Convert(access, typeof(object));
            return System.Linq.Expressions.Expression.Lambda<Func<object, object?>>(boxed, param).Compile();
        }

        return null;
    }

    // ── Segment cache ──────────────────────────────────────────────────────────

    private static (string Segment, bool NullSafe)[] GetSegments(string path)
        => _segmentCache.GetOrAdd(path, static p => ParseSegments(p));

    private static (string Segment, bool NullSafe)[] ParseSegments(string path)
    {
        // Strip any leading ../ parent navigation — those are handled by EvalProperty
        // before calling TryResolve, so they never reach segment parsing.
        while (path.StartsWith("../", StringComparison.Ordinal))
            path = path[3..];

        //
        // nullSafe=true on a segment means: "if this segment is null OR missing, return null
        // instead of propagating a resolution failure". The ?. operator guards the segment it
        // follows — i.e. Customer?.Name means Customer is null-safe, not Name.
        //
        // Examples:
        //   "Customer.Name"         → [("Customer",false), ("Name",false)]
        //   "Customer?.Name"        → [("Customer",true),  ("Name",false)]
        //   "A?.B?.C"               → [("A",true), ("B",true), ("C",false)]
        //   "A.B?.C?.D"             → [("A",false), ("B",true), ("C",true), ("D",false)]

        var result = new List<(string, bool)>(4);
        int pos = 0;

        while (pos < path.Length)
        {
            // Find the next separator: '.' or '?.'
            int dotPos = path.IndexOf('.', pos);
            int nullSafePos = path.IndexOf("?.", pos, StringComparison.Ordinal);

            if (dotPos == -1 && nullSafePos == -1)
            {
                // Last segment, no more separators
                result.Add((path[pos..], false));
                break;
            }

            // Determine which separator comes first
            bool useNullSafe;
            int sepPos;
            if (nullSafePos != -1 && (dotPos == -1 || nullSafePos < dotPos))
            {
                useNullSafe = true;
                sepPos = nullSafePos;
            }
            else
            {
                useNullSafe = false;
                sepPos = dotPos;
            }

            var segment = path[pos..sepPos];
            if (segment.Length > 0)
                result.Add((segment, false));

            // Advance past separator
            pos = sepPos + (useNullSafe ? 2 : 1);

            // The segment AFTER a ?. separator is null-safe
            if (useNullSafe && pos < path.Length)
            {
                // Find end of this null-safe segment
                int nextDot = path.IndexOf('.', pos);
                int nextNullSafe = path.IndexOf("?.", pos, StringComparison.Ordinal);
                int end;
                if (nextDot == -1 && nextNullSafe == -1) end = path.Length;
                else if (nextNullSafe != -1 && (nextDot == -1 || nextNullSafe <= nextDot)) end = nextNullSafe;
                else end = nextDot;

                result.Add((path[pos..end], true));
                pos = end;
                if (pos < path.Length && path[pos] == '.') pos++; // skip plain dot after null-safe segment
            }
        }

        return result.ToArray();
    }
}