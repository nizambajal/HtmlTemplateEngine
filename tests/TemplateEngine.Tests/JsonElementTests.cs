using System.Dynamic;
using System.Text.Json;
using FluentAssertions;
using TemplateEngine.Exceptions;
using Xunit;

namespace TemplateEngine.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// JsonElement Tests
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tests for rendering templates against <see cref="System.Text.Json.JsonElement"/> models.
/// Covers flat objects, nested objects, arrays, null values, and all value kinds.
/// </summary>
public class JsonElementTests
{
    private readonly HtmlTemplateEngine _engine = new();

    // ── Primitive value kinds ─────────────────────────────────────────────────

    [Fact]
    public void StringProperty_RendersValue()
    {
        var model = Json("""{ "Name": "Nizam" }""");
        _engine.Render(model, "{{Name}}").Should().Be("Nizam");
    }

    [Fact]
    public void IntegerProperty_RendersValue()
    {
        var model = Json("""{ "Age": 28 }""");
        _engine.Render(model, "{{Age}}").Should().Be("28");
    }

    [Fact]
    public void FloatProperty_RendersValue()
    {
        var model = Json("""{ "Score": 98.6 }""");
        _engine.Render(model, "{{Score}}").Should().Be("98.6");
    }

    [Fact]
    public void BoolTrue_RendersValue()
    {
        var model = Json("""{ "Active": true }""");
        _engine.Render(model, "{{Active}}").Should().Be("True");
    }

    [Fact]
    public void BoolFalse_RendersValue()
    {
        var model = Json("""{ "Active": false }""");
        _engine.Render(model, "{{Active}}").Should().Be("False");
    }

    [Fact]
    public void NullProperty_RendersEmpty()
    {
        var model = Json("""{ "Name": null }""");
        _engine.Render(model, "{{Name}}").Should().Be(string.Empty);
    }

    // ── Property case-insensitivity ───────────────────────────────────────────

    [Fact]
    public void PropertyName_IsCaseInsensitive()
    {
        var model = Json("""{ "firstName": "Nizam" }""");
        _engine.Render(model, "{{FirstName}}").Should().Be("Nizam");
    }

    [Fact]
    public void PropertyName_UpperCaseJsonLowerCaseTemplate()
    {
        var model = Json("""{ "CITY": "Mangaluru" }""");
        _engine.Render(model, "{{city}}").Should().Be("Mangaluru");
    }

    // ── Nested objects ────────────────────────────────────────────────────────

    [Fact]
    public void NestedObject_TwoLevels()
    {
        var model = Json("""{ "Customer": { "Name": "Nizam" } }""");
        _engine.Render(model, "{{Customer.Name}}").Should().Be("Nizam");
    }

    [Fact]
    public void NestedObject_ThreeLevels()
    {
        var model = Json("""{ "Customer": { "Address": { "City": "Mangaluru" } } }""");
        _engine.Render(model, "{{Customer.Address.City}}").Should().Be("Mangaluru");
    }

    [Fact]
    public void NestedObject_FourLevels()
    {
        var model = Json("""{ "A": { "B": { "C": { "D": "deep" } } } }""");
        _engine.Render(model, "{{A.B.C.D}}").Should().Be("deep");
    }

    [Fact]
    public void MultiplePropertiesInTemplate()
    {
        var model = Json("""{ "First": "John", "Last": "Doe", "Age": 30 }""");
        _engine.Render(model, "{{First}} {{Last}}, age {{Age}}")
            .Should().Be("John Doe, age 30");
    }

    // ── Null-safe navigation ──────────────────────────────────────────────────

    [Fact]
    public void NullSafe_NullJsonValue_ReturnsEmpty()
    {
        var model = Json("""{ "Customer": null }""");
        _engine.Render(model, "{{Customer?.Name}}").Should().Be(string.Empty);
    }

    [Fact]
    public void NullSafe_MissingProperty_ReturnsEmpty()
    {
        var model = Json("""{ "Customer": {} }""");
        _engine.Render(model, "{{Customer?.Name}}").Should().Be(string.Empty);
    }

    [Fact]
    public void NullSafe_DeepChain_ReturnsEmpty()
    {
        var model = Json("""{ "Customer": null }""");
        _engine.Render(model, "{{Customer?.Address?.City}}").Should().Be(string.Empty);
    }

    // ── HTML encoding ─────────────────────────────────────────────────────────

    [Fact]
    public void StringWithHtml_IsEncoded()
    {
        var model = Json("""{ "Content": "<b>bold</b>" }""");
        _engine.Render(model, "{{Content}}").Should().Be("&lt;b&gt;bold&lt;/b&gt;");
    }

    [Fact]
    public void RawExpression_SkipsEncoding()
    {
        var model = Json("""{ "Html": "<b>bold</b>" }""");
        _engine.Render(model, "{{{Html}}}").Should().Be("<b>bold</b>");
    }

    // ── Conditionals ──────────────────────────────────────────────────────────

    [Fact]
    public void If_BoolTrue_RendersThenBranch()
    {
        var model = Json("""{ "IsActive": true }""");
        _engine.Render(model, "{{#if IsActive}}Active{{/if}}").Should().Be("Active");
    }

    [Fact]
    public void If_BoolFalse_RendersElseBranch()
    {
        var model = Json("""{ "IsActive": false }""");
        _engine.Render(model, "{{#if IsActive}}yes{{else}}no{{/if}}").Should().Be("no");
    }

    [Fact]
    public void If_NullValue_RendersElseBranch()
    {
        var model = Json("""{ "Name": null }""");
        _engine.Render(model, "{{#if Name}}has{{else}}empty{{/if}}").Should().Be("empty");
    }

    [Fact]
    public void If_StringEquality_Matches()
    {
        var model = Json("""{ "Role": "Admin" }""");
        _engine.Render(model, """{{#if Role=="Admin"}}A{{else}}B{{/if}}""").Should().Be("A");
    }

    [Fact]
    public void If_NumericComparison_GreaterThan()
    {
        var model = Json("""{ "Age": 25 }""");
        _engine.Render(model, "{{#if Age > 18}}Adult{{else}}Minor{{/if}}").Should().Be("Adult");
    }

    [Fact]
    public void ElseIf_SelectsMatchingBranch()
    {
        var model = Json("""{ "Role": "Editor" }""");
        _engine.Render(model,
            """{{#if Role=="Admin"}}Admin{{else if Role=="Editor"}}Editor{{else}}User{{/if}}""")
            .Should().Be("Editor");
    }

    [Fact]
    public void NestedIf_BothConditionsTrue()
    {
        var model = Json("""{ "Active": true, "Verified": true }""");
        _engine.Render(model,
            "{{#if Active}}{{#if Verified}}OK{{else}}Unverified{{/if}}{{else}}Inactive{{/if}}")
            .Should().Be("OK");
    }

    // ── Arrays and foreach ────────────────────────────────────────────────────

    [Fact]
    public void Array_Count_ReturnsLength()
    {
        var model = Json("""{ "Items": [1, 2, 3] }""");
        _engine.Render(model, "{{Items.Count}}").Should().Be("3");
    }

    [Fact]
    public void Array_ForeachWithAlias_RendersAllItems()
    {
        var model = Json("""{ "Tags": ["alpha", "beta", "gamma"] }""");
        _engine.Render(model, "{{#foreach Tags as tag}}{{tag}},{{/foreach}}")
            .Should().Be("alpha,beta,gamma,");
    }

    [Fact]
    public void Array_ForeachObjectItems_AccessProperties()
    {
        var model = Json("""
            {
                "Orders": [
                    { "Number": "ORD-001", "Total": 100 },
                    { "Number": "ORD-002", "Total": 200 }
                ]
            }
            """);
        _engine.Render(model, "{{#foreach Orders as o}}{{o.Number}}:{{o.Total}} {{/foreach}}")
            .Should().Be("ORD-001:100 ORD-002:200 ");
    }

    [Fact]
    public void Array_ForeachLoopIndex()
    {
        var model = Json("""{ "Items": ["a", "b", "c"] }""");
        _engine.Render(model, "{{#foreach Items as item}}{{@index}}{{/foreach}}")
            .Should().Be("012");
    }

    [Fact]
    public void Array_ForeachLoopCount()
    {
        var model = Json("""{ "Items": ["a", "b", "c"] }""");
        _engine.Render(model, "{{#foreach Items as item}}{{@count}}{{/foreach}}")
            .Should().Be("333");
    }

    [Fact]
    public void Array_ForeachFirstAndLast()
    {
        var model = Json("""{ "Items": ["a", "b", "c"] }""");
        var template = "{{#foreach Items as item}}{{#if @first}}[{{/if}}{{item}}{{#if @last}}]{{/if}}{{/foreach}}";
        _engine.Render(model, template).Should().Be("[abc]");
    }

    [Fact]
    public void Array_ForeachParentContext()
    {
        var model = Json("""{ "Title": "Report", "Items": [{ "Name": "Item1" }] }""");
        _engine.Render(model, "{{#foreach Items as item}}{{../Title}}:{{item.Name}}{{/foreach}}")
            .Should().Be("Report:Item1");
    }

    [Fact]
    public void Array_NestedForeach()
    {
        var model = Json("""
            {
                "Categories": [
                    {
                        "Name": "Electronics",
                        "Products": [
                            { "Name": "Phone" },
                            { "Name": "Laptop" }
                        ]
                    }
                ]
            }
            """);
        _engine.Render(model,
            "{{#foreach Categories as cat}}{{cat.Name}}:{{#foreach cat.Products as p}}{{p.Name}},{{/foreach}}{{/foreach}}")
            .Should().Be("Electronics:Phone,Laptop,");
    }

    [Fact]
    public void Array_EmptyArray_RendersNothing()
    {
        var model = Json("""{ "Items": [] }""");
        _engine.Render(model, "{{#foreach Items as item}}{{item}}{{/foreach}}")
            .Should().Be(string.Empty);
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    [Fact]
    public void NumberFormat_F2_OnJsonNumber()
    {
        var model = Json("""{ "Price": 1234.5 }""");
        _engine.Render(model, "{{Price:F2}}").Should().Be("1234.50");
    }

    [Fact]
    public void StringFormat_Uppercase_OnJsonString()
    {
        var model = Json("""{ "Name": "hello" }""");
        _engine.Render(model, "{{Name:U}}").Should().Be("HELLO");
    }

    // ── Non-generic Render(object) overload ───────────────────────────────────

    [Fact]
    public void NonGenericRender_AcceptsJsonElementAsObject()
    {
        object model = Json("""{ "Name": "Nizam" }""");
        _engine.Render(model, "{{Name}}").Should().Be("Nizam");
    }

    // ── ResolveProperty ───────────────────────────────────────────────────────

    [Fact]
    public void ResolveProperty_OnJsonElement_ReturnsValue()
    {
        var model = Json("""{ "Customer": { "Name": "Nizam" } }""");
        _engine.ResolveProperty(model, "Customer.Name").Should().Be("Nizam");
    }

    private static JsonElement Json(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);
}

// ─────────────────────────────────────────────────────────────────────────────
// Dictionary Tests
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tests for rendering templates against <see cref="Dictionary{TKey,TValue}"/> models.
/// Covers string keys, mixed value types, nested dictionaries, and foreach loops.
/// </summary>
public class DictionaryModelTests
{
    private readonly HtmlTemplateEngine _engine = new();

    // ── Basic property access ─────────────────────────────────────────────────

    [Fact]
    public void StringValue_RendersCorrectly()
    {
        var model = new Dictionary<string, object?> { ["Name"] = "Alice" };
        _engine.Render(model, "{{Name}}").Should().Be("Alice");
    }

    [Fact]
    public void IntValue_RendersCorrectly()
    {
        var model = new Dictionary<string, object?> { ["Age"] = 30 };
        _engine.Render(model, "{{Age}}").Should().Be("30");
    }

    [Fact]
    public void BoolValue_ConditionEvaluatesCorrectly()
    {
        var model = new Dictionary<string, object?> { ["Active"] = true };
        _engine.Render(model, "{{#if Active}}yes{{/if}}").Should().Be("yes");
    }

    [Fact]
    public void NullValue_RendersEmpty()
    {
        var model = new Dictionary<string, object?> { ["Name"] = null };
        _engine.Render(model, "{{Name}}").Should().Be(string.Empty);
    }

    [Fact]
    public void NullValue_ConditionIsFalse()
    {
        var model = new Dictionary<string, object?> { ["Name"] = null };
        _engine.Render(model, "{{#if Name}}yes{{else}}no{{/if}}").Should().Be("no");
    }

    [Fact]
    public void MultipleKeys_AllRenderCorrectly()
    {
        var model = new Dictionary<string, object?>
        {
            ["First"] = "John",
            ["Last"] = "Doe",
            ["Age"] = 30
        };
        _engine.Render(model, "{{First}} {{Last}}, age {{Age}}")
            .Should().Be("John Doe, age 30");
    }

    // ── Case sensitivity ──────────────────────────────────────────────────────

    [Fact]
    public void KeyLookup_IsCaseInsensitive()
    {
        var model = new Dictionary<string, object?> { ["firstName"] = "Alice" };
        _engine.Render(model, "{{FirstName}}").Should().Be("Alice");
    }

    // ── Nested dictionary ─────────────────────────────────────────────────────

    [Fact]
    public void NestedDictionary_AccessesInnerValue()
    {
        var model = new Dictionary<string, object?>
        {
            ["Customer"] = new Dictionary<string, object?> { ["Name"] = "Bob" }
        };
        _engine.Render(model, "{{Customer.Name}}").Should().Be("Bob");
    }

    [Fact]
    public void NestedDictionary_ThreeLevelsDeep()
    {
        var model = new Dictionary<string, object?>
        {
            ["Order"] = new Dictionary<string, object?>
            {
                ["Customer"] = new Dictionary<string, object?> { ["City"] = "NYC" }
            }
        };
        _engine.Render(model, "{{Order.Customer.City}}").Should().Be("NYC");
    }

    // ── Conditionals ──────────────────────────────────────────────────────────

    [Fact]
    public void If_StringEquality()
    {
        var model = new Dictionary<string, object?> { ["Role"] = "Admin" };
        _engine.Render(model, """{{#if Role=="Admin"}}A{{else}}B{{/if}}""").Should().Be("A");
    }

    [Fact]
    public void If_NumericComparison()
    {
        var model = new Dictionary<string, object?> { ["Score"] = 85 };
        _engine.Render(model, "{{#if Score >= 80}}Pass{{else}}Fail{{/if}}").Should().Be("Pass");
    }

    [Fact]
    public void ElseIf_FallsThrough_ToElse()
    {
        var model = new Dictionary<string, object?> { ["Status"] = "Pending" };
        _engine.Render(model,
            """{{#if Status=="Active"}}A{{else if Status=="Inactive"}}I{{else}}P{{/if}}""")
            .Should().Be("P");
    }

    // ── Foreach ───────────────────────────────────────────────────────────────

    [Fact]
    public void Foreach_OverListValue()
    {
        var model = new Dictionary<string, object?>
        {
            ["Items"] = new List<object?> { "X", "Y", "Z" }
        };
        _engine.Render(model, "{{#foreach Items as item}}{{item}}{{/foreach}}")
            .Should().Be("XYZ");
    }

    [Fact]
    public void Foreach_OverObjectList_AccessProperties()
    {
        var model = new Dictionary<string, object?>
        {
            ["Products"] = new List<Dictionary<string, object?>>
            {
                new() { ["Name"] = "Widget", ["Price"] = 9.99 },
                new() { ["Name"] = "Gadget", ["Price"] = 19.99 }
            }
        };
        _engine.Render(model, "{{#foreach Products as p}}{{p.Name}},{{/foreach}}")
            .Should().Be("Widget,Gadget,");
    }

    [Fact]
    public void Foreach_ParentContext_AccessesOuterKey()
    {
        var model = new Dictionary<string, object?>
        {
            ["Title"] = "Catalog",
            ["Items"] = new List<object?> { "A", "B" }
        };
        _engine.Render(model, "{{#foreach Items as item}}{{../Title}}:{{item}} {{/foreach}}")
            .Should().Be("Catalog:A Catalog:B ");
    }

    [Fact]
    public void Foreach_LoopMetadata_IndexAndCount()
    {
        var model = new Dictionary<string, object?> { ["Items"] = new List<object?> { "a", "b", "c" } };
        _engine.Render(model, "{{#foreach Items as item}}{{@index}}/{{@count}} {{/foreach}}")
            .Should().Be("0/3 1/3 2/3 ");
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    [Fact]
    public void Format_NumericValue()
    {
        var model = new Dictionary<string, object?> { ["Amount"] = 1234.5 };
        _engine.Render(model, "{{Amount:F2}}").Should().Be("1234.50");
    }

    [Fact]
    public void Format_StringUpper()
    {
        var model = new Dictionary<string, object?> { ["Name"] = "hello" };
        _engine.Render(model, "{{Name:U}}").Should().Be("HELLO");
    }

    // ── Non-generic overload ──────────────────────────────────────────────────

    [Fact]
    public void NonGenericRender_AcceptsDictionaryAsObject()
    {
        object model = new Dictionary<string, object?> { ["Name"] = "Charlie" };
        _engine.Render(model, "{{Name}}").Should().Be("Charlie");
    }

    // ── ResolveProperty ───────────────────────────────────────────────────────

    [Fact]
    public void ResolveProperty_OnDictionary_ReturnsValue()
    {
        var model = new Dictionary<string, object?> { ["City"] = "Mangaluru" };
        _engine.ResolveProperty(model, "City").Should().Be("Mangaluru");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ExpandoObject Tests
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tests for rendering templates against <see cref="ExpandoObject"/> models.
/// ExpandoObject implements IDictionary&lt;string, object?&gt; internally so
/// it exercises the same resolution path as dictionary but via dynamic syntax.
/// </summary>
public class ExpandoObjectTests
{
    private readonly HtmlTemplateEngine _engine = new();

    // ── Basic property access ─────────────────────────────────────────────────

    [Fact]
    public void SimpleProperty_RendersValue()
    {
        dynamic model = new ExpandoObject();
        model.Name = "Dana";
        _engine.Render((object)model, "{{Name}}").Should().Be("Dana");
    }

    [Fact]
    public void NumericProperty_RendersValue()
    {
        dynamic model = new ExpandoObject();
        model.Age = 25;
        _engine.Render((object)model, "{{Age}}").Should().Be("25");
    }

    [Fact]
    public void BoolProperty_ConditionTrue()
    {
        dynamic model = new ExpandoObject();
        model.Active = true;
        _engine.Render((object)model, "{{#if Active}}yes{{/if}}").Should().Be("yes");
    }

    [Fact]
    public void BoolProperty_ConditionFalse_RendersElse()
    {
        dynamic model = new ExpandoObject();
        model.Active = false;
        _engine.Render((object)model, "{{#if Active}}yes{{else}}no{{/if}}").Should().Be("no");
    }

    [Fact]
    public void MultipleProperties_AllRenderCorrectly()
    {
        dynamic model = new ExpandoObject();
        model.First = "Jane";
        model.Last = "Smith";
        model.Age = 28;
        _engine.Render((object)model, "{{First}} {{Last}}, {{Age}}")
            .Should().Be("Jane Smith, 28");
    }

    // ── Nested ExpandoObject ──────────────────────────────────────────────────

    [Fact]
    public void NestedExpando_TwoLevels()
    {
        dynamic inner = new ExpandoObject();
        inner.City = "Mangaluru";

        dynamic model = new ExpandoObject();
        model.Address = inner;

        _engine.Render((object)model, "{{Address.City}}").Should().Be("Mangaluru");
    }

    [Fact]
    public void NestedExpando_MixedWithPoco()
    {
        dynamic model = new ExpandoObject();
        model.Name = "Eve";
        model.Address = new { City = "London", PostCode = "SW1" };

        _engine.Render((object)model, "{{Name}} in {{Address.City}}")
            .Should().Be("Eve in London");
    }

    // ── Conditionals ──────────────────────────────────────────────────────────

    [Fact]
    public void If_StringEquality()
    {
        dynamic model = new ExpandoObject();
        model.Role = "Admin";
        _engine.Render((object)model, """{{#if Role=="Admin"}}Admin{{else}}User{{/if}}""")
            .Should().Be("Admin");
    }

    [Fact]
    public void If_NumericComparison()
    {
        dynamic model = new ExpandoObject();
        model.Score = 72;
        _engine.Render((object)model, "{{#if Score >= 70}}Pass{{else}}Fail{{/if}}")
            .Should().Be("Pass");
    }

    // ── Foreach ───────────────────────────────────────────────────────────────

    [Fact]
    public void Foreach_OverListProperty()
    {
        dynamic model = new ExpandoObject();
        model.Tags = new List<string> { "csharp", "dotnet", "oss" };

        _engine.Render((object)model, "{{#foreach Tags as tag}}{{tag}},{{/foreach}}")
            .Should().Be("csharp,dotnet,oss,");
    }

    [Fact]
    public void Foreach_OverExpandoList()
    {
        dynamic item1 = new ExpandoObject(); item1.Name = "Alpha"; item1.Price = 10;
        dynamic item2 = new ExpandoObject(); item2.Name = "Beta"; item2.Price = 20;

        dynamic model = new ExpandoObject();
        model.Items = new List<object> { item1, item2 };

        _engine.Render((object)model, "{{#foreach Items as item}}{{item.Name}}:{{item.Price}} {{/foreach}}")
            .Should().Be("Alpha:10 Beta:20 ");
    }

    [Fact]
    public void Foreach_ParentContext()
    {
        dynamic model = new ExpandoObject();
        model.Owner = "Acme";
        model.Items = new List<string> { "X", "Y" };

        _engine.Render((object)model, "{{#foreach Items as item}}{{../Owner}}:{{item}} {{/foreach}}")
            .Should().Be("Acme:X Acme:Y ");
    }

    // ── HTML encoding ─────────────────────────────────────────────────────────

    [Fact]
    public void StringWithHtml_IsEncoded()
    {
        dynamic model = new ExpandoObject();
        model.Content = "<script>alert('xss')</script>";
        _engine.Render((object)model, "{{Content}}")
            .Should().Be("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;");
    }

    // ── Non-generic overload ──────────────────────────────────────────────────

    [Fact]
    public void NonGenericRender_AcceptsExpandoAsObject()
    {
        dynamic expando = new ExpandoObject();
        expando.City = "Paris";
        object model = expando;
        _engine.Render(model, "{{City}}").Should().Be("Paris");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Anonymous Type Tests
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tests for rendering against C# anonymous types via the non-generic
/// <c>Render(object, string)</c> overload.
/// </summary>
public class AnonymousTypeTests
{
    private readonly HtmlTemplateEngine _engine = new();

    [Fact]
    public void AnonymousType_SimpleProperty()
    {
        object model = new { Name = "Frank", Age = 40 };
        _engine.Render(model, "{{Name}} is {{Age}}").Should().Be("Frank is 40");
    }

    [Fact]
    public void AnonymousType_NestedProperty()
    {
        object model = new { Customer = new { Name = "Grace", City = "Berlin" } };
        _engine.Render(model, "{{Customer.Name}} from {{Customer.City}}")
            .Should().Be("Grace from Berlin");
    }

    [Fact]
    public void AnonymousType_BoolCondition()
    {
        object model = new { IsAdmin = true, Name = "Hal" };
        _engine.Render(model, "{{#if IsAdmin}}{{Name}} is admin{{/if}}")
            .Should().Be("Hal is admin");
    }

    [Fact]
    public void AnonymousType_Foreach()
    {
        object model = new { Items = new[] { new { Name = "P1" }, new { Name = "P2" } } };
        _engine.Render(model, "{{#foreach Items as item}}{{item.Name}},{{/foreach}}")
            .Should().Be("P1,P2,");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Mixed Model Tests (cross-type scenarios)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Tests where different model types are mixed — e.g. a POCO containing
/// a Dictionary, or a foreach over a JsonElement array whose items are POCOs.
/// </summary>
public class MixedModelTests
{
    private readonly HtmlTemplateEngine _engine = new();

    [Fact]
    public void Poco_ContainingDictionary_AccessesKeyAsProperty()
    {
        var model = new
        {
            Name = "Ivan",
            Settings = new Dictionary<string, string> { ["Theme"] = "Dark" }
        };
        _engine.Render(model, "{{Name}} — {{Settings.Theme}}").Should().Be("Ivan — Dark");
    }

    [Fact]
    public void Dictionary_ContainingPoco_AccessesPocoProperty()
    {
        var model = new Dictionary<string, object?>
        {
            ["Title"] = "Invoice",
            ["Customer"] = new { Name = "Julia", City = "Rome" }
        };
        _engine.Render(model, "{{Title}} for {{Customer.Name}} in {{Customer.City}}")
            .Should().Be("Invoice for Julia in Rome");
    }

    [Fact]
    public void JsonElement_MixedWithPocoInForeach()
    {
        // JsonElement array items mixed with POCO list
        var jsonModel = JsonSerializer.Deserialize<JsonElement>("""
            { "Label": "Products", "Items": [{ "Name": "Alpha" }, { "Name": "Beta" }] }
            """);

        _engine.Render(jsonModel,
            "{{Label}}: {{#foreach Items as item}}{{item.Name}} {{/foreach}}")
            .Should().Be("Products: Alpha Beta ");
    }

    [Fact]
    public void SameTemplate_DifferentModelTypes_AllProduceCorrectOutput()
    {
        const string template = "Hello {{Name}}!";

        // POCO
        _engine.Render(new { Name = "POCO" }, template).Should().Be("Hello POCO!");

        // Dictionary
        _engine.Render(new Dictionary<string, object?> { ["Name"] = "Dict" }, template)
            .Should().Be("Hello Dict!");

        // ExpandoObject
        dynamic expando = new ExpandoObject();
        expando.Name = "Expando";
        _engine.Render((object)expando, template).Should().Be("Hello Expando!");

        // JsonElement
        _engine.Render(
            JsonSerializer.Deserialize<JsonElement>("""{ "Name": "Json" }"""), template)
            .Should().Be("Hello Json!");
    }

    [Fact]
    public void Foreach_NestedMixedTypes_ParentContextWorks()
    {
        // Root is a Dictionary, items are POCOs
        var model = new Dictionary<string, object?>
        {
            ["Company"] = "Acme",
            ["Staff"] = new List<object>
            {
                new { Name = "Karl" },
                new { Name = "Lena" }
            }
        };

        _engine.Render(model,
            "{{#foreach Staff as person}}{{../Company}}:{{person.Name}} {{/foreach}}")
            .Should().Be("Acme:Karl Acme:Lena ");
    }
}