using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;

namespace TemplateEngine.Tests;

/// <summary>Performance benchmarks and large-template stress tests.</summary>
public class PerformanceTests
{
    private readonly HtmlTemplateEngine _engine = new();

    [Fact]
    public void SmallTemplate_CachedRender_IsSubMillisecond()
    {
        var model = new { Name = "Nizam", Count = 42 };
        var template = "<h1>{{Name}}</h1><p>Count: {{Count}}</p>";

        // Warm up cache
        _engine.Render(model, template);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
            _engine.Render(model, template);
        sw.Stop();

        var avg = sw.Elapsed.TotalMilliseconds / 10_000;
        avg.Should().BeLessThan(1.0, "cached render should be sub-millisecond");
    }

    [Fact]
    public void LargeTemplate_RendersWithin5Seconds()
    {
        // Build a large template with 1000 items
        var items = Enumerable.Range(1, 1000)
            .Select(i => new { Id = i, Name = $"Item {i}", Active = i % 2 == 0, Price = i * 1.5 })
            .ToList();

        var model = new { Items = items, Title = "Large Report" };

        var template = new StringBuilder();
        template.Append("<html><body><h1>{{Title}}</h1><table>");
        template.Append("{{#foreach Items as item}}");
        template.Append("<tr><td>{{item.Id}}</td><td>{{item.Name}}</td>");
        template.Append("{{#if item.Active}}<td>Active</td>{{else}}<td>Inactive</td>{{/if}}");
        template.Append("<td>{{item.Price:N2}}</td></tr>");
        template.Append("{{/foreach}}");
        template.Append("</table></body></html>");

        var sw = Stopwatch.StartNew();
        var result = _engine.Render(model, template.ToString());
        sw.Stop();

        sw.Elapsed.TotalSeconds.Should().BeLessThan(5);
        result.Should().Contain("Item 1");
        result.Should().Contain("Item 1000");
        result.Should().Contain("Active");
        result.Should().Contain("Inactive");
    }

    [Fact]
    public void ConcurrentRenders_ThreadSafe()
    {
        var model = new { Name = "Concurrent", Count = 7 };
        var template = "<p>{{Name}} {{Count}}</p>";

        // Prime the cache
        _engine.Render(model, template);

        var errors = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            try { _engine.Render(model, template); }
            catch (Exception ex) { errors.Add(ex); }
        }));

        Task.WaitAll(tasks.ToArray());
        errors.Should().BeEmpty("concurrent renders must not throw");
    }

    [Fact]
    public void VeryLargeString_RendersFast()
    {
        var longText = new string('A', 100_000);
        var model = new { Content = longText };
        // Static content around an expression
        var template = "<div>" + new string('X', 10_000) + "{{Content}}" + new string('Y', 10_000) + "</div>";

        var sw = Stopwatch.StartNew();
        var result = _engine.Render(model, template);
        sw.Stop();

        sw.Elapsed.TotalSeconds.Should().BeLessThan(2);
        result.Should().Contain(longText);
    }
}
