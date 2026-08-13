using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Domain.Entities;

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public MovementType Type { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int? PurchaseOrderId { get; set; }
    public int? TransactionId { get; set; }
    /// <summary>Receipt number of the sale this movement belongs to — lets a refund find the exact
    /// sold lines to reverse (item-level, capped at what was sold). Empty for non-sale movements.</summary>
    public string ReceiptNo { get; set; } = string.Empty;
    /// <summary>The player this sale line was sold to (if any) — for per-player sale history / refunds.</summary>
    public int? RelatedEmployeeId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product? Product { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
}
