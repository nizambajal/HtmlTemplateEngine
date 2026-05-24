using System.Collections.Concurrent;
using System.Globalization;

namespace TemplateEngine.Formatting;

/// <summary>
/// Applies format specifiers to values.
/// Built-in formats:
/// <list type="table">
///   <item><term>yyyy-MM-dd (or any date format)</term><description>Date/time format strings</description></item>
///   <item><term>C</term><description>Currency</description></item>
///   <item><term>N2, N0 etc.</term><description>Numeric</description></item>
///   <item><term>U</term><description>UpperCase string</description></item>
///   <item><term>L</term><description>LowerCase string</description></item>
///   <item><term>0000 etc.</term><description>Custom numeric format</description></item>
/// </list>
/// Custom formatters can be registered at runtime.
/// </summary>
public sealed class FormatterEngine
{
    private readonly ConcurrentDictionary<string, Func<object?, string>> _formatters = new(StringComparer.OrdinalIgnoreCase);

    public FormatterEngine()
    {
        // Built-in string formatters
        RegisterFormatter("U", v => v?.ToString()?.ToUpperInvariant() ?? string.Empty);
        RegisterFormatter("L", v => v?.ToString()?.ToLowerInvariant() ?? string.Empty);
    }

    /// <summary>Registers a custom named formatter.</summary>
    public void RegisterFormatter(string name, Func<object?, string> formatter)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        _formatters[name] = formatter ?? throw new ArgumentNullException(nameof(formatter));
    }

    /// <summary>Applies <paramref name="format"/> to <paramref name="value"/>.</summary>
    public string Format(object? value, string format)
    {
        if (value == null) return string.Empty;
        if (string.IsNullOrEmpty(format)) return value.ToString() ?? string.Empty;

        // Check custom formatters first
        if (_formatters.TryGetValue(format, out var customFmt))
            return customFmt(value);

        // Date/time
        if (value is DateTime dt)
            return dt.ToString(format, CultureInfo.InvariantCulture);
        if (value is DateTimeOffset dto)
            return dto.ToString(format, CultureInfo.InvariantCulture);
        if (value is DateOnly dateOnly)
            return dateOnly.ToString(format, CultureInfo.InvariantCulture);

        // Numeric standard/custom
        if (IsNumeric(value))
        {
            try
            {
                var d = Convert.ToDouble(value);
                return format.ToUpperInvariant() switch
                {
                    "C" => d.ToString("C", CultureInfo.CurrentCulture),
                    _ => d.ToString(format, CultureInfo.InvariantCulture)
                };
            }
            catch
            {
                return value.ToString() ?? string.Empty;
            }
        }

        // IFormattable fallback
        if (value is IFormattable formattable)
            return formattable.ToString(format, CultureInfo.InvariantCulture);

        return value.ToString() ?? string.Empty;
    }

    private static bool IsNumeric(object? v) =>
        v is int or long or double or float or decimal or short or byte or uint or ulong;
}
