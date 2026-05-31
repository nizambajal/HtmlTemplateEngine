# Sniz.HtmlTemplateEngine

A production-grade C# HTML template engine built on a real **Lexer → Parser → AST → Renderer** pipeline. Inspired by Handlebars, but custom-built for .NET 8 with full extensibility, thread safety, and parse caching.

---

## Features

- ✅ Property binding with deep nesting and null-safe navigation
- ✅ Conditional blocks — `{{#if}}`, `{{else if}}`, `{{else}}`, unlimited nesting
- ✅ Foreach loops with alias, loop metadata (`@index`, `@first`, `@last`, `@count`)
- ✅ Parent context navigation (`../Property`, `../../Property`)
- ✅ Format specifiers — dates, currency, numeric, upper/lower, custom
- ✅ Arithmetic and string expressions — `{{Qty * Price}}`, `{{First + " " + Last}}`
- ✅ HTML encoding by default, raw output via `{{{expr}}}`
- ✅ Custom helpers and formatters
- ✅ Parse cache — templates are parsed once and reused across renders
- ✅ Thread-safe concurrent rendering

---

## Project Structure

```
src/TemplateEngine/
├── Abstractions/       Base class + IHelperRegistry + IFormatterRegistry
├── Ast/                AST node types (DocumentNode, IfNode, ForeachNode, …)
├── Caching/            ConcurrentDictionary-backed parse cache
├── Context/            ContextStack + LoopContext (foreach metadata)
├── Evaluator/          ExpressionEvaluator + PropertyResolver (compiled getters)
├── Exceptions/         TemplateParseException, RenderingException, …
├── Formatting/         FormatterEngine (built-in + extensible)
├── Lexer/              TemplateLexer, Token, TokenType
├── Parser/             Recursive-descent TemplateParser
├── Rendering/          Renderer (AST walker)
└── HtmlTemplateEngine.cs   Public API
```

---

## Quick Start

```csharp
var engine = new HtmlTemplateEngine();

var model = new
{
    Customer = new { Name = "Nizam", IsActive = true },
    CreatedDate = DateTime.Now
};

string html = engine.Render(model, "<h1>Hello, {{Customer.Name}}!</h1>");
// → <h1>Hello, Nizam!</h1>
```

---

## Usage

### 1. Property Binding

```csharp
// Simple
engine.Render(new { Name = "Alice" }, "Hello {{Name}}!");

// Nested
engine.Render(model, "{{Customer.Address.City}}");

// Null-safe (returns empty string instead of throwing)
engine.Render(model, "{{Customer?.Address?.City}}");

// Dictionary
engine.Render(new { Settings = new Dictionary<string,string>{{ "Theme","Dark" }} },
    "Theme: {{Settings.Theme}}");

// Collection count
engine.Render(new { Items = new[] { 1, 2, 3 } }, "Total: {{Items.Count}}");

// Resolve a single property directly
string name = engine.ResolveProperty(customer, "Address.City"); // → "Mangaluru"
```

---

### 2. HTML Encoding

```csharp
// Default — HTML encoded (safe)
engine.Render(new { Value = "<script>alert('xss')</script>" }, "{{Value}}");
// → &lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;

// Raw — unescaped output
engine.Render(new { Html = "<b>bold</b>" }, "{{{Html}}}");
// → <b>bold</b>
```

---

### 3. Conditionals

```csharp
var template = """
    {{#if User.IsActive}}
        {{#if User.Role == "Admin"}}
            <span>Admin Panel</span>
        {{else if User.Role == "Editor"}}
            <span>Editor Panel</span>
        {{else}}
            <span>Dashboard</span>
        {{/if}}
    {{else}}
        <span>Account Disabled</span>
    {{/if}}
    """;

// Supported operators: == != > < >= <= && || !
engine.Render(new { User = new { IsActive = true, Role = "Admin" } }, template);
```

---

### 4. Foreach Loops

```csharp
// Basic loop — access properties directly
var template = """
    {{#foreach Orders}}
        <li>{{OrderNumber}}</li>
    {{/foreach}}
    """;

// With alias
var template = """
    {{#foreach Orders as order}}
        <li>{{order.OrderNumber}} — {{order.Customer.Name}}</li>
    {{/foreach}}
    """;

// Loop metadata
var template = """
    {{#foreach Items as item}}
        {{@index}}. {{item.Name}}
        {{#if @first}}<hr>{{/if}}
        {{#if @last}}<hr>{{/if}}
        ({{@count}} total)
    {{/foreach}}
    """;
```

---

### 5. Nested Loops + Parent Context

```csharp
var template = """
    {{#foreach Categories as category}}
        <h2>{{category.Name}}</h2>
        {{#foreach category.Products as product}}
            <p>
                Category: {{../category.Name}}
                Product:  {{product.Name}}
                Customer: {{../../Customer.Name}}
            </p>
        {{/foreach}}
    {{/foreach}}
    """;
```

---

### 6. Formatting

```csharp
// Date
engine.Render(new { Date = DateTime.Now },    "{{Date:yyyy-MM-dd}}");

// Currency
engine.Render(new { Price = 1299.99m },       "{{Price:C}}");

// Numeric
engine.Render(new { Amount = 9876.5 },        "{{Amount:N2}}");    // → 9876.50

// Zero-padded
engine.Render(new { Id = 42 },               "{{Id:0000}}");       // → 0042

// String case
engine.Render(new { Name = "hello" },         "{{Name:U}}");       // → HELLO
engine.Render(new { Name = "HELLO" },         "{{Name:L}}");       // → hello

// Custom formatter
engine.RegisterFormatter("Truncate", v => v?.ToString()?[..10] ?? "");
engine.Render(new { Bio = "Long text here" }, "{{Bio:Truncate}}");
```

---

### 7. Expressions

```csharp
// Arithmetic
engine.Render(new { Qty = 3, Price = 10.0 },         "{{Qty * Price}}");   // → 30
engine.Render(new { A = 100.0, B = 30.0 },           "{{A - B}}");         // → 70

// String concatenation
engine.Render(new { First = "John", Last = "Doe" },  "{{First + \" \" + Last}}");

// Boolean in conditions
engine.Render(new { Age = 20 }, "{{#if Age >= 18}}Adult{{/if}}");
```

---

### 8. Custom Helpers

```csharp
// Value helper — {{HelperName(Expression)}}
engine.RegisterHelper("Upper",  v => v?.ToString()?.ToUpper());
engine.RegisterHelper("Slugify", v => v?.ToString()?.ToLower().Replace(" ", "-"));
engine.RegisterHelper("Truncate100", v => {
    var s = v?.ToString() ?? "";
    return s.Length > 100 ? s[..100] + "…" : s;
});

engine.Render(new { Name = "hello world" }, "{{Upper(Name)}}");    // → HELLO WORLD
engine.Render(new { Title = "My Blog Post" }, "{{Slugify(Title)}}"); // → my-blog-post
```

---

### 9. Error Handling

All exceptions include line number, column, and the offending token.

```csharp
try
{
    engine.Render(model, "{{#if User.}}");   // malformed expression
}
catch (TemplateSyntaxException ex)
{
    Console.WriteLine(ex.Message);
    // → Unexpected token '.' in expression (Line 1, Column 11)
    //   Token: .
}

// Exception types:
// TemplateParseException     — lexer/tokenizer errors
// TemplateSyntaxException    — parser structure errors
// PropertyResolutionException — property not found on model
// RenderingException          — runtime render errors
```

---

### 10. Template Caching

Templates are parsed once and cached automatically. No extra configuration needed.

```csharp
var engine = new HtmlTemplateEngine(cacheCapacity: 2000); // default: 1000

engine.Render(model, template); // parsed + cached
engine.Render(model, template); // served from cache — no re-parse

Console.WriteLine(engine.CachedTemplateCount); // → 1
engine.ClearCache();
```

---

### 11. Thread Safety

The engine instance is safe to share across threads. Each `Render()` call creates its own context stack and renderer internally.

```csharp
// Shared singleton — safe for concurrent use
var engine = new HtmlTemplateEngine();

Parallel.ForEach(requests, req =>
{
    var html = engine.Render(req.Model, req.Template);
});
```

---

## Running Tests

```bash
dotnet restore
dotnet test
```

Test coverage includes:

- Property binding (simple, nested, deep, dictionary, null-safe, count)
- Conditional blocks (if, else-if, else, nested, all operators)
- Foreach loops (alias, no-alias, metadata, nested, parent context, empty/null)
- Formatting (date, currency, numeric, string case, custom)
- Expressions (arithmetic, string concat, boolean)
- Custom helpers and formatters
- Null handling
- Error handling and exception types
- Parse caching
- Concurrent rendering (thread safety)
- Performance (large templates, 10 000+ iteration loops)

---

## Requirements

- .NET 8.0+
- No third-party runtime dependencies
