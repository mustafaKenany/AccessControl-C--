namespace AccessControlPro.Application.DTOs;

public class SupplierBalanceDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalRemaining => TotalAmount - TotalDiscount - TotalPaid;
}
