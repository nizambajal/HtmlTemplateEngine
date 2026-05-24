namespace TemplateEngine.Abstractions;

/// <summary>
/// Core abstraction for all template engines.
/// Implementations must be thread-safe when using the shared cache.
/// </summary>
public abstract class TemplateEngine
{
    /// <summary>
    /// Renders <paramref name="template"/> against <paramref name="model"/> and returns the
    /// resulting string. Parsing is cached so repeated calls with the same template string
    /// do not re-parse.
    /// </summary>
    public abstract string Render<T>(T model, string template);

    /// <summary>
    /// Resolves a single dotted property expression from <paramref name="model"/>.
    /// Example: <c>ResolveProperty(model, "Customer.Name")</c> → <c>"Nizam"</c>
    /// </summary>
    public abstract string ResolveProperty(object model, string propertyExpression);
}

/// <summary>Contract for custom helper registration.</summary>
public interface IHelperRegistry
{
    /// <summary>Registers a value helper: <c>{{HelperName(Arg)}}</c>.</summary>
    void RegisterHelper(string name, Func<object?, object?> helper);
}

/// <summary>Contract for custom formatter registration.</summary>
public interface IFormatterRegistry
{
    /// <summary>Registers a named formatter: <c>{{Value:FormatName}}</c>.</summary>
    void RegisterFormatter(string name, Func<object?, string> formatter);
}
