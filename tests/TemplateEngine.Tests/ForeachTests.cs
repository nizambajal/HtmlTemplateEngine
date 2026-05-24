using FluentAssertions;
using TemplateEngine.Tests.Models;
using Xunit;

namespace TemplateEngine.Tests;

/// <summary>Tests for foreach loops, aliasing, nesting, and loop metadata.</summary>
public class ForeachTests
{
    private readonly HtmlTemplateEngine _engine = new();

    [Fact]
    public void BasicForeach_RendersAllItems()
    {
        var model = new { Items = new List<string> { "A", "B", "C" } };
        // Without alias, current item is a string so we access it directly
        var template = "{{#foreach Items as item}}{{item}}{{/foreach}}";
        _engine.Render(model, template).Should().Be("ABC");
    }

    [Fact]
    public void ForeachWithAlias_AccessesProperties()
    {
        var model = new
        {
            Orders = new List<object>
            {
                new { Name = "Alpha" },
                new { Name = "Beta" }
            }
        };
        var template = "{{#foreach Orders as order}}{{order.Name}},{{/foreach}}";
        _engine.Render(model, template).Should().Be("Alpha,Beta,");
    }

    [Fact]
    public void ForeachWithoutAlias_AccessesPropertiesDirectly()
    {
        var model = new
        {
            Items = new List<object>
            {
                new { Name = "X" },
                new { Name = "Y" }
            }
        };
        var template = "{{#foreach Items}}{{Name}}-{{/foreach}}";
        _engine.Render(model, template).Should().Be("X-Y-");
    }

    [Fact]
    public void LoopMetadata_Index_StartsAtZero()
    {
        var model = new { Items = new List<string> { "A", "B", "C" } };
        var template = "{{#foreach Items as item}}{{@index}}{{/foreach}}";
        _engine.Render(model, template).Should().Be("012");
    }

    [Fact]
    public void LoopMetadata_Count_IsTotal()
    {
        var model = new { Items = new List<string> { "A", "B", "C" } };
        var template = "{{#foreach Items as item}}{{@count}}{{/foreach}}";
        _engine.Render(model, template).Should().Be("333");
    }

    [Fact]
    public void LoopMetadata_First_TrueOnlyFirst()
    {
        var model = new { Items = new List<string> { "A", "B", "C" } };
        var template = "{{#foreach Items as item}}{{#if @first}}F{{/if}}{{/foreach}}";
        _engine.Render(model, template).Should().Be("F");
    }

    [Fact]
    public void LoopMetadata_Last_TrueOnlyLast()
    {
        var model = new { Items = new List<string> { "A", "B", "C" } };
        var template = "{{#foreach Items as item}}{{#if @last}}L{{/if}}{{/foreach}}";
        _engine.Render(model, template).Should().Be("L");
    }

    [Fact]
    public void NestedForeach_AccessesCorrectProperties()
    {
        var model = new
        {
            Categories = new List<CategoryModel>
            {
                new CategoryModel
                {
                    CategoryName = "Electronics",
                    Products = new List<ProductModel>
                    {
                        new ProductModel { Name = "Phone" },
                        new ProductModel { Name = "Laptop" }
                    }
                }
            }
        };

        var template = "{{#foreach Categories as category}}{{category.CategoryName}}:{{#foreach category.Products as product}}{{product.Name}},{{/foreach}}{{/foreach}}";
        _engine.Render(model, template).Should().Be("Electronics:Phone,Laptop,");
    }

    [Fact]
    public void ParentContext_AccessesOuterModel()
    {
        var model = new
        {
            Customer = new Customer("Nizam", true, "Admin"),
            Orders = new List<object>
            {
                new { OrderNumber = "ORD-001" }
            }
        };

        var template = "{{#foreach Orders as order}}{{../Customer.Name}}-{{order.OrderNumber}}{{/foreach}}";
        _engine.Render(model, template).Should().Be("Nizam-ORD-001");
    }

    [Fact]
    public void DoubleParentContext_AccessesGrandparentModel()
    {
        var model = new
        {
            Customer = new Customer("Nizam", true, "Admin"),
            Orders = new List<object>
            {
                new
                {
                    OrderNumber = "ORD-001",
                    Items = new List<object> { new { Name = "Widget" } }
                }
            }
        };

        var template = "{{#foreach Orders as order}}{{#foreach order.Items as item}}{{../../Customer.Name}}:{{../OrderNumber}}:{{item.Name}}{{/foreach}}{{/foreach}}";
        _engine.Render(model, template).Should().Be("Nizam:ORD-001:Widget");
    }

    [Fact]
    public void EmptyCollection_RendersNothing()
    {
        var model = new { Items = new List<string>() };
        _engine.Render(model, "{{#foreach Items as item}}{{item}}{{/foreach}}").Should().Be(string.Empty);
    }

    [Fact]
    public void NullCollection_RendersNothing()
    {
        var model = new { Items = (List<string>?)null };
        _engine.Render(model, "{{#foreach Items as item}}{{item}}{{/foreach}}").Should().Be(string.Empty);
    }
}
