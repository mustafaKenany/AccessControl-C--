namespace AccessControlPro.Application.DTOs;

/// <summary>A past POS sale (one receipt) shown in the "refund from a recent sale" picker.
/// Ported from the HikVision fork.</summary>
public class PosSaleDto
{
    public string ReceiptNo { get; set; } = string.Empty;
    public DateTime Time { get; set; }
    public decimal Total { get; set; }               // net total originally sold
    public decimal RefundableTotal { get; set; }     // value still refundable (sold − already refunded)
    public bool FullyRefunded => RefundableTotal <= 0;
    public string ItemsSummary { get; set; } = string.Empty;
    public List<PosSaleLineDto> Lines { get; set; } = new();
}

public class PosSaleLineDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }           // net unit price actually paid on the sale
    public int SoldQty { get; set; }
    public int RefundedQty { get; set; }
    public int RefundableQty => SoldQty - RefundedQty;

    // UI helper: how many of RefundableQty the cashier wants to return now (0..RefundableQty).
    public int RefundNowQty { get; set; }
}
