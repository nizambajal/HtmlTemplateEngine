using System.Collections.Concurrent;
using TemplateEngine.Ast;

namespace TemplateEngine.Caching;

/// <summary>
/// Thread-safe cache for parsed template ASTs.
/// Cache key is based on the template string content.
/// </summary>
public sealed class TemplateCache
{
    private readonly ConcurrentDictionary<string, DocumentNode> _cache = new();
    private readonly int _maxEntries;

    public TemplateCache(int maxEntries = 1000)
    {
        _maxEntries = maxEntries;
    }

    /// <summary>
    /// Returns a cached AST if one exists for <paramref name="template"/>;
    /// otherwise invokes <paramref name="factory"/>, caches the result, and returns it.
    /// </summary>
    public DocumentNode GetOrAdd(string template, Func<string, DocumentNode> factory)
    {
        if (_cache.TryGetValue(template, out var cached))
            return cached;

        var doc = factory(template);

        // Evict if over limit (simple strategy: clear all)
        if (_cache.Count >= _maxEntries) _cache.Clear();

        _cache[template] = doc;
        return doc;
    }

    /// <summary>Clears the entire cache.</summary>
    public void Clear() => _cache.Clear();

    /// <summary>Number of cached entries.</summary>
    public int Count => _cache.Count;
}
