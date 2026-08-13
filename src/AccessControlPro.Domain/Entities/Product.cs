namespace AccessControlPro.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal Price { get; set; }          // sell price (what the customer pays)
    public decimal CostPrice { get; set; }      // buy price (latest purchase cost) — for profit/margin
    public string Category { get; set; } = string.Empty;
    public int Stock { get; set; }
    /// <summary>Low-stock threshold. When Stock &lt;= this, the product surfaces in low-stock alerts. 0 = no alert.</summary>
    public int ReorderLevel { get; set; }
    /// <summary>Optional expiry date. A product past its expiry is blocked from being sold and surfaces in expiry alerts.</summary>
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
