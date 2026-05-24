using TemplateEngine.Abstractions;
using TemplateEngine.Ast;
using TemplateEngine.Caching;
using TemplateEngine.Context;
using TemplateEngine.Evaluator;
using TemplateEngine.Exceptions;
using TemplateEngine.Formatting;
using TemplateEngine.Lexer;
using TemplateEngine.Parser;
using TemplateEngine.Rendering;

namespace TemplateEngine;

/// <summary>
/// Production-grade HTML template engine.
/// Thread-safe: helper/formatter registration should be done before multi-threaded use,
/// but rendering is fully concurrent because each call creates its own renderer + context stack.
/// </summary>
public sealed class HtmlTemplateEngine : Abstractions.TemplateEngine, IHelperRegistry, IFormatterRegistry
{
    private readonly TemplateCache _cache;
    private readonly FormatterEngine _formatter;
    private readonly PropertyResolver _propertyResolver;

    // Value helpers: name -> (value -> result)
    private readonly Dictionary<string, Func<object?, object?>> _helpers = new(StringComparer.OrdinalIgnoreCase);

    // Block helpers (future extension)
    private readonly Dictionary<string, Func<string, ContextStack, string>> _blockHelpers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initialises the engine with default settings and an optional cache capacity.
    /// </summary>
    /// <param name="cacheCapacity">Maximum number of parsed templates to keep in memory. Default 1000.</param>
    public HtmlTemplateEngine(int cacheCapacity = 1000)
    {
        _cache = new TemplateCache(cacheCapacity);
        _formatter = new FormatterEngine();
        _propertyResolver = new PropertyResolver();
    }

    // ── IHelperRegistry ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void RegisterHelper(string name, Func<object?, object?> helper)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        _helpers[name] = helper ?? throw new ArgumentNullException(nameof(helper));
    }

    // ── IFormatterRegistry ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void RegisterFormatter(string name, Func<object?, string> formatter)
        => _formatter.RegisterFormatter(name, formatter);

    // ── TemplateEngine overrides ───────────────────────────────────────────────

    /// <inheritdoc/>
    public override string Render<T>(T model, string template)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));

        var doc = GetOrParseTemplate(template);
        var ctx = new ContextStack(model);
        var evaluator = new ExpressionEvaluator(_propertyResolver, _helpers);
        var renderer = new Renderer(evaluator, _formatter, _blockHelpers);
        return renderer.Render(doc, ctx);
    }

    /// <inheritdoc/>
    public override string ResolveProperty(object model, string propertyExpression)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (string.IsNullOrWhiteSpace(propertyExpression)) return string.Empty;

        var result = _propertyResolver.Resolve(model, propertyExpression);
        return result?.ToString() ?? string.Empty;
    }

    // ── Block helper registration (extensibility) ─────────────────────────────

    /// <summary>
    /// Registers a block helper for <c>{{#HelperName}}...{{/HelperName}}</c> syntax.
    /// </summary>
    public void RegisterBlockHelper(string name, Func<string, ContextStack, string> helper)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        _blockHelpers[name] = helper ?? throw new ArgumentNullException(nameof(helper));
    }

    /// <summary>Clears the parsed template cache.</summary>
    public void ClearCache() => _cache.Clear();

    /// <summary>Number of templates currently in the parse cache.</summary>
    public int CachedTemplateCount => _cache.Count;

    // ── Internal ──────────────────────────────────────────────────────────────

    private DocumentNode GetOrParseTemplate(string template)
    {
        return _cache.GetOrAdd(template, src =>
        {
            try
            {
                var lexer = new TemplateLexer(src);
                var tokens = lexer.Tokenize();
                var parser = new Parser.TemplateParser(tokens);
                return parser.Parse();
            }
            catch (TemplateEngineException) { throw; }
            catch (Exception ex)
            {
                throw new TemplateParseException("Unexpected error during template parsing", 0, 0, null, ex);
            }
        });
    }
}
