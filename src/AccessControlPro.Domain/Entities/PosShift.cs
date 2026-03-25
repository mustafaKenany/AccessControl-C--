namespace AccessControlPro.Domain.Entities;

public class PosShift
{
    public int Id { get; set; }
    public string OpenedBy { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal ClosingCash { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalCashSales { get; set; }
    public decimal TotalCardSales { get; set; }
    public decimal Variance { get; set; }
    public string Status { get; set; } = "Open"; // Open, Closed
}
