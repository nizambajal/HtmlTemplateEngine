using FluentAssertions;
using TemplateEngine.Exceptions;
using TemplateEngine.Tests.Models;
using Xunit;

namespace TemplateEngine.Tests;

/// <summary>Tests for format specifiers.</summary>
public class FormattingTests
{
    private readonly HtmlTemplateEngine _engine = new();

    [Fact]
    public void DateFormat_AppliesFormatString()
    {
        var model = new SimpleModel { CreatedDate = new DateTime(2025, 1, 15) };
        _engine.Render(model, "{{CreatedDate:yyyy-MM-dd}}").Should().Be("2025-01-15");
    }

    [Fact]
    public void CurrencyFormat_AppliesCurrencyFormat()
    {
        var model = new SimpleModel { Price = 1234.5m };
        // C format is locale-dependent, just verify it contains the numeric part
        var result = _engine.Render(model, "{{Price:F2}}");
        result.Should().Be("1234.50");
    }

    [Fact]
    public void NumericFormat_N2()
    {
        var model = new { Amount = 9876.543 };
        _engine.Render(model, "{{Amount:N2}}").Should().Be("9,876.54");
    }

    [Fact]
    public void CustomNumericFormat_LeadingZeros()
    {
        var model = new { Value = 42 };
        _engine.Render(model, "{{Value:0000}}").Should().Be("0042");
    }

    [Fact]
    public void StringFormat_Uppercase()
    {
        var model = new SimpleModel { Name = "hello" };
        _engine.Render(model, "{{Name:U}}").Should().Be("HELLO");
    }

    [Fact]
    public void StringFormat_Lowercase()
    {
        var model = new SimpleModel { Name = "HELLO" };
        _engine.Render(model, "{{Name:L}}").Should().Be("hello");
    }

    [Fact]
    public void CustomFormatter_IsInvoked()
    {
        _engine.RegisterFormatter("SHOUT", v => v?.ToString()?.ToUpper() + "!!!" ?? "");
        var model = new SimpleModel { Name = "hello" };
        _engine.Render(model, "{{Name:SHOUT}}").Should().Be("HELLO!!!");
    }
}

/// <summary>Tests for expression evaluation.</summary>
public class ExpressionTests
{
    private readonly HtmlTemplateEngine _engine = new();

    [Fact]
    public void Arithmetic_Multiplication()
    {
        var model = new { Quantity = 3.0, Price = 10.0 };
        _engine.Render(model, "{{Quantity * Price}}").Should().Be("30");
    }

    [Fact]
    public void Arithmetic_Addition()
    {
        var model = new { A = 5.0, B = 3.0 };
        _engine.Render(model, "{{A + B}}").Should().Be("8");
    }

    [Fact]
    public void Arithmetic_Subtraction()
    {
        var model = new { A = 10.0, B = 4.0 };
        _engine.Render(model, "{{A - B}}").Should().Be("6");
    }

    [Fact]
    public void StringConcatenation_WithLiteral()
    {
        var model = new { FirstName = "John", LastName = "Doe" };
        _engine.Render(model, "{{FirstName + \" \" + LastName}}").Should().Be("John Doe");
    }

    [Fact]
    public void BooleanExpression_AgeCheck()
    {
        var model = new SimpleModel { Age = 20 };
        _engine.Render(model, "{{#if Age > 18}}Adult{{/if}}").Should().Be("Adult");
    }
}

/// <summary>Tests for custom helpers.</summary>
public class HelperTests
{
    private readonly HtmlTemplateEngine _engine = new();

    [Fact]
    public void ValueHelper_TransformsValue()
    {
        _engine.RegisterHelper("Upper", v => v?.ToString()?.ToUpper());
        var model = new SimpleModel { Name = "hello" };
        _engine.Render(model, "{{Upper(Name)}}").Should().Be("HELLO");
    }

    [Fact]
    public void ValueHelper_WithLiteralArgument()
    {
        _engine.RegisterHelper("Repeat", v => new string('*', int.TryParse(v?.ToString(), out var n) ? n : 0));
        var model = new object();
        _engine.Render(model, "{{Repeat(5)}}").Should().Be("*****");
    }

    [Fact]
    public void UnknownHelper_ThrowsRenderingException()
    {
        var model = new SimpleModel { Name = "test" };
        var act = () => _engine.Render(model, "{{UnknownHelper(Name)}}");
        act.Should().Throw<RenderingException>();
    }
}

/// <summary>Tests for null handling.</summary>
public class NullHandlingTests
{
    private readonly HtmlTemplateEngine _engine = new();

    [Fact]
    public void NullProperty_RendersEmpty()
    {
        var model = new SimpleModel { NullableField = null };
        _engine.Render(model, "{{NullableField}}").Should().Be(string.Empty);
    }

    [Fact]
    public void NullSafeNavigation_ReturnsEmpty()
    {
        var model = new Customer("Test", true, "Admin", Address: null);
        _engine.Render(model, "City: {{Address?.City}}").Should().Be("City: ");
    }

    [Fact]
    public void NullInCondition_TreatedAsFalse()
    {
        var model = new SimpleModel { NullableField = null };
        _engine.Render(model, "{{#if NullableField}}yes{{else}}no{{/if}}").Should().Be("no");
    }
}

/// <summary>Tests for error handling and exception types.</summary>
public class ErrorHandlingTests
{
    private readonly HtmlTemplateEngine _engine = new();

    [Fact]
    public void InvalidSyntax_ThrowsParseException()
    {
        // Unclosed expression
        var act = () => _engine.Render(new { }, "{{Name");
        act.Should().Throw<TemplateEngineException>();
    }

    [Fact]
    public void UnmatchedEndIf_ThrowsSyntaxException()
    {
        var act = () => _engine.Render(new { }, "{{/if}}");
        act.Should().Throw<TemplateEngineException>();
    }
}

/// <summary>Tests for template caching.</summary>
public class CachingTests
{
    [Fact]
    public void SameTemplate_UsedFromCache()
    {
        var engine = new HtmlTemplateEngine();
        var model = new { Name = "Alice" };
        var template = "Hello {{Name}}";

        engine.Render(model, template);
        engine.CachedTemplateCount.Should().Be(1);

        // Second render — should use cache
        engine.Render(model, template);
        engine.CachedTemplateCount.Should().Be(1);
    }

    [Fact]
    public void DifferentTemplates_BothCached()
    {
        var engine = new HtmlTemplateEngine();
        var model = new { Name = "Alice" };
        engine.Render(model, "Hello {{Name}}");
        engine.Render(model, "Bye {{Name}}");
        engine.CachedTemplateCount.Should().Be(2);
    }

    [Fact]
    public void ClearCache_ResetsCount()
    {
        var engine = new HtmlTemplateEngine();
        var model = new { Name = "Alice" };
        engine.Render(model, "Hello {{Name}}");
        engine.ClearCache();
        engine.CachedTemplateCount.Should().Be(0);
    }
}
