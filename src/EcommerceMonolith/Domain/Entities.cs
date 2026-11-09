using System.Text.Json.Serialization;

namespace Monolith.Domain;

// ---------------------------------------------------------------------------
// Domain model shared by every service of the monolith (single schema).
// ---------------------------------------------------------------------------

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Emoji { get; set; } = "📦";
    public decimal Price { get; set; }
    public bool Active { get; set; } = true;
}

/// <summary>Owned by the Inventory service (stock lives apart from the catalog).</summary>
public class InventoryItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public int ReorderLevel { get; set; } = 5;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Cart
{
    public string Id { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<CartItem> Items { get; set; } = [];
}

public class CartItem
{
    public int Id { get; set; }
    public string CartId { get; set; } = "";
    [JsonIgnore] public Cart? Cart { get; set; }
    public int ProductId { get; set; }
    [JsonIgnore] public Product? Product { get; set; }
    public int Quantity { get; set; }
}

public enum OrderStatus
{
    Pending,   // created, waiting for payment confirmation
    Paid,      // payment approved + stock reserved
    Shipped,
    Cancelled  // payment declined (or stock problem)
}

public enum PaymentStatus
{
    Approved,
    Declined
}

public class Order
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<OrderItem> Items { get; set; } = [];
    public Payment? Payment { get; set; }
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    [JsonIgnore] public Order? Order { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Emoji { get; set; } = "📦";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    [JsonIgnore] public Order? Order { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "card";
    public PaymentStatus Status { get; set; }
    public string TransactionId { get; set; } = "";
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
