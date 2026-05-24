using FluentAssertions;
using TemplateEngine.Tests.Models;
using Xunit;

namespace TemplateEngine.Tests;

/// <summary>Tests for property binding, nested access, null-safety, dictionary and collection count.</summary>
public class PropertyBindingTests
{
    private readonly HtmlTemplateEngine _engine = new();

    [Fact]
    public void SimpleProperty_RendersValue()
    {
        var model = new SimpleModel { Name = "Nizam" };
        _engine.Render(model, "Hello {{Name}}!").Should().Be("Hello Nizam!");
    }

    [Fact]
    public void NestedProperty_RendersValue()
    {
        var customer = new Customer("Nizam", true, "Admin", new Address("Main St", "Mangaluru"));
        var model = new { Customer = customer };
        _engine.Render(model, "{{Customer.Name}} from {{Customer.Address.City}}")
            .Should().Be("Nizam from Mangaluru");
    }

    [Fact]
    public void DeepNesting_ResolvesCorrectly()
    {
        var order = new Order("ORD-001",
            new Customer("Nizam", true, "Admin", new Address("Main St", "Mangaluru")),
            new List<OrderItem>(), DateTime.UtcNow);
        var model = new { Order = order };
        _engine.Render(model, "{{Order.Customer.Address.City}}")
            .Should().Be("Mangaluru");
    }

    [Fact]
    public void DictionaryAccess_ResolvesValue()
    {
        var model = new { Settings = new Dictionary<string, string> { ["Theme"] = "Dark" } };
        _engine.Render(model, "{{Settings.Theme}}").Should().Be("Dark");
    }

    [Fact]
    public void NullSafe_ReturnsEmptyOnNull()
    {
        var model = new Customer("Nizam", true, "Admin", Address: null);
        // Using null-safe navigation so no exception
        var template = "{{Address?.City}}";
        _engine.Render(model, template).Should().Be(string.Empty);
    }

    [Fact]
    public void CollectionCount_ReturnsCount()
    {
        var model = new { Items = new List<string> { "a", "b", "c" } };
        _engine.Render(model, "{{Items.Count}}").Should().Be("3");
    }

    [Fact]
    public void ResolveProperty_ReturnsSingleValue()
    {
        var customer = new Customer("Nizam", true, "Admin");
        _engine.ResolveProperty(customer, "Name").Should().Be("Nizam");
    }

    [Fact]
    public void HtmlEncoding_EscapesHtml()
    {
        var model = new { Value = "<script>alert('xss')</script>" };
        _engine.Render(model, "{{Value}}")
            .Should().Be("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;");
    }

    [Fact]
    public void RawExpression_RendersUnescaped()
    {
        var model = new { Html = "<b>bold</b>" };
        _engine.Render(model, "{{{Html}}}").Should().Be("<b>bold</b>");
    }

    [Fact]
    public void MultipleProperties_InSameTemplate()
    {
        var model = new SimpleModel { Name = "Alice", Age = 30 };
        _engine.Render(model, "{{Name}} is {{Age}} years old.")
            .Should().Be("Alice is 30 years old.");
    }
}
