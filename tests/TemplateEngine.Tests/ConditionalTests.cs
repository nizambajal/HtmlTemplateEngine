using FluentAssertions;
using TemplateEngine.Tests.Models;
using Xunit;

namespace TemplateEngine.Tests;

/// <summary>Tests for if/else/else-if blocks with unlimited nesting.</summary>
public class ConditionalTests
{
    private readonly HtmlTemplateEngine _engine = new();

    [Fact]
    public void SimpleIf_True_RendersBody()
    {
        var model = new SimpleModel { IsActive = true };
        var result = _engine.Render(model, "{{#if IsActive}}Active{{/if}}");
        result.Should().Be("Active");
    }

    [Fact]
    public void SimpleIf_False_RendersNothing()
    {
        var model = new SimpleModel { IsActive = false };
        _engine.Render(model, "{{#if IsActive}}Active{{/if}}").Should().Be(string.Empty);
    }

    [Fact]
    public void IfElse_FalseBranch_RendersElse()
    {
        var model = new SimpleModel { IsActive = false };
        _engine.Render(model, "{{#if IsActive}}Yes{{else}}No{{/if}}").Should().Be("No");
    }

    [Fact]
    public void ElseIf_SelectsCorrectBranch()
    {
        var model = new Customer("Nizam", true, "Editor");
        var template = "{{#if Role==\"Admin\"}}Admin{{else if Role==\"Editor\"}}Editor{{else}}User{{/if}}";
        _engine.Render(model, template).Should().Be("Editor");
    }

    [Fact]
    public void ElseIf_FallsToElse()
    {
        var model = new Customer("Nizam", true, "Guest");
        var template = "{{#if Role==\"Admin\"}}Admin{{else if Role==\"Editor\"}}Editor{{else}}User{{/if}}";
        _engine.Render(model, template).Should().Be("User");
    }

    [Fact]
    public void NestedIf_BothTrue()
    {
        var model = new Customer("Nizam", true, "Admin");
        var template = "{{#if IsActive}}{{#if Role==\"Admin\"}}AdminActive{{else}}Active{{/if}}{{else}}Disabled{{/if}}";
        _engine.Render(model, template).Should().Be("AdminActive");
    }

    [Fact]
    public void NestedIf_OuterFalse_InnerNotEvaluated()
    {
        var model = new Customer("Nizam", false, "Admin");
        var template = "{{#if IsActive}}{{#if Role==\"Admin\"}}AdminActive{{/if}}{{else}}Disabled{{/if}}";
        _engine.Render(model, template).Should().Be("Disabled");
    }

    [Fact]
    public void NestedIf_ThreeLevelsDeep()
    {
        var model = new { A = true, B = true, C = true };
        var template = "{{#if A}}{{#if B}}{{#if C}}Deep{{/if}}{{/if}}{{/if}}";
        _engine.Render(model, template).Should().Be("Deep");
    }

    [Fact]
    public void ComparisonOperator_NotEqual()
    {
        var model = new SimpleModel { Age = 25 };
        _engine.Render(model, "{{#if Age!=18}}Not18{{/if}}").Should().Be("Not18");
    }

    [Fact]
    public void ComparisonOperator_GreaterThan()
    {
        var model = new SimpleModel { Age = 21 };
        _engine.Render(model, "{{#if Age>18}}Adult{{/if}}").Should().Be("Adult");
    }

    [Fact]
    public void LogicalAnd_BothTrue()
    {
        var model = new Customer("Nizam", true, "Admin");
        _engine.Render(model, "{{#if IsActive&&Role==\"Admin\"}}OK{{/if}}").Should().Be("OK");
    }

    [Fact]
    public void LogicalOr_OneTrue()
    {
        var model = new Customer("Nizam", false, "Admin");
        _engine.Render(model, "{{#if IsActive||Role==\"Admin\"}}OK{{/if}}").Should().Be("OK");
    }

    [Fact]
    public void Negation_False_IsTrue()
    {
        var model = new SimpleModel { IsActive = false };
        _engine.Render(model, "{{#if !IsActive}}Inactive{{/if}}").Should().Be("Inactive");
    }

    [Fact]
    public void NullCheck_NullValue_FalseCondition()
    {
        var model = new SimpleModel { NullableField = null };
        _engine.Render(model, "{{#if NullableField}}HasValue{{else}}NoValue{{/if}}").Should().Be("NoValue");
    }
}
