namespace AccessControlPro.Application.DTOs;

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal Price { get; set; }       // sell price
    public decimal CostPrice { get; set; }   // buy price (latest purchase cost)
    public string Category { get; set; } = string.Empty;
    public int Stock { get; set; }
    public int ReorderLevel { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Unit profit margin (sell - cost). Negative if selling below cost.</summary>
    public decimal Profit => Price - CostPrice;
    /// <summary>Current stock valued at cost — total tied-up capital for this product.</summary>
    public decimal StockValue => CostPrice * Stock;
}
