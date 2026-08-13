using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Domain.Entities;

public class Transaction
{
    public int Id { get; set; }
    public TransactionType Type { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? RelatedEmployeeId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public decimal DiscountAmount { get; set; }
    public string DiscountReason { get; set; } = string.Empty;
    /// <summary>Optional link back to a source document, e.g. "PO-12" for a purchase-order expense. Lets the entry stay in sync when that document is edited.</summary>
    public string Reference { get; set; } = string.Empty;
    /// <summary>Unique per-sale receipt number (e.g. "R-20260813-0007"). Ties a POS-sale transaction
    /// to its stock movements so a refund can reverse the exact original sale. Empty for non-sale rows.</summary>
    public string ReceiptNo { get; set; } = string.Empty;

    public Employee? RelatedEmployee { get; set; }
}
