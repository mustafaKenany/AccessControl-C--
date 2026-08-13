namespace AccessControlPro.Application.DTOs;

/// <summary>One row of the product-level sales report: how much of a product sold in a period,
/// net of refunds, with revenue, cost and profit. Built from stock movements + product cost.
/// Ported from the HikVision fork.</summary>
public class ProductSalesReportItemDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public int QtySold { get; set; }
    public int QtyReturned { get; set; }
    public int NetQty => QtySold - QtyReturned;

    /// <summary>Net revenue = gross sales minus refunds (at the sold unit price).</summary>
    public decimal Revenue { get; set; }
    /// <summary>Cost of the net units sold, at the product's recorded cost.</summary>
    public decimal Cost { get; set; }
    public decimal Profit => Revenue - Cost;
    public decimal MarginPercent => Revenue == 0 ? 0 : Math.Round(Profit / Revenue * 100, 1);
}
