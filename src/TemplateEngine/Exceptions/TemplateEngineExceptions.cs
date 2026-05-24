namespace TemplateEngine.Exceptions;

/// <summary>
/// Base exception for all template engine errors.
/// </summary>
public abstract class TemplateEngineException : Exception
{
    /// <summary>Line number where the error occurred (1-based).</summary>
    public int Line { get; }

    /// <summary>Column number where the error occurred (1-based).</summary>
    public int Column { get; }

    /// <summary>The token or expression that caused the error.</summary>
    public string? Token { get; }

    protected TemplateEngineException(string message, int line, int column, string? token = null, Exception? inner = null)
        : base(FormatMessage(message, line, column, token), inner)
    {
        Line = line;
        Column = column;
        Token = token;
    }

    private static string FormatMessage(string message, int line, int column, string? token)
    {
        var tokenPart = token != null ? $"\n  Token: {token}" : string.Empty;
        return $"{message} (Line {line}, Column {column}){tokenPart}";
    }
}

/// <summary>
/// Thrown when the template contains a parse error.
/// </summary>
public sealed class TemplateParseException : TemplateEngineException
{
    public TemplateParseException(string message, int line, int column, string? token = null, Exception? inner = null)
        : base(message, line, column, token, inner) { }
}

/// <summary>
/// Thrown when the template contains invalid syntax.
/// </summary>
public sealed class TemplateSyntaxException : TemplateEngineException
{
    public TemplateSyntaxException(string message, int line, int column, string? token = null, Exception? inner = null)
        : base(message, line, column, token, inner) { }
}

/// <summary>
/// Thrown when a property expression cannot be resolved against the model.
/// </summary>
public sealed class PropertyResolutionException : TemplateEngineException
{
    /// <summary>The expression that failed to resolve.</summary>
    public string Expression { get; }

    public PropertyResolutionException(string expression, int line, int column, string? details = null, Exception? inner = null)
        : base($"Cannot resolve property '{expression}'{(details != null ? ": " + details : "")}", line, column, expression, inner)
    {
        Expression = expression;
    }
}

/// <summary>
/// Thrown when an error occurs during rendering.
/// </summary>
public sealed class RenderingException : TemplateEngineException
{
    public RenderingException(string message, int line, int column, string? token = null, Exception? inner = null)
        : base(message, line, column, token, inner) { }
}
