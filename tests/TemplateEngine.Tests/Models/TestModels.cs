namespace TemplateEngine.Tests.Models;

public record Address(string Street, string City, string? PostCode = null);

public record Customer(
    string Name,
    bool IsActive,
    string Role,
    Address? Address = null,
    Dictionary<string, string>? Settings = null);

public record OrderItem(string Name, decimal Price, int Quantity);

public record Order(
    string OrderNumber,
    Customer Customer,
    List<OrderItem> Items,
    DateTime CreatedDate);

public record Invoice(
    string InvoiceNumber,
    Customer Customer,
    List<Order> Orders,
    decimal TotalAmount);

public class SimpleModel
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
    public string? NullableField { get; set; }
    public string RawHtml { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public decimal Price { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class CategoryModel
{
    public string CategoryName { get; set; } = string.Empty;
    public List<ProductModel> Products { get; set; } = new();
}

public class ProductModel
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
