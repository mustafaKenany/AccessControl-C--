namespace AccessControlPro.Application.DTOs;

/// <summary>End-of-day / shift cashier report (X = live snapshot of the open shift, Z = the closing
/// report). Shows exactly how the expected drawer cash is built up. Ported from the HikVision fork.</summary>
public class ShiftReportDto
{
    public string OpenedBy { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public DateTime AsOf { get; set; }
    public bool IsClosed { get; set; }

    public decimal OpeningCash { get; set; }
    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal CashTopUps { get; set; }
    public decimal CashDebtPayments { get; set; }
    public decimal CashRefunds { get; set; }

    /// <summary>Opening + cash sales + cash top-ups + cash debt-payments − cash refunds.</summary>
    public decimal ExpectedCash { get; set; }

    public decimal TotalSales { get; set; }
    public int SalesCount { get; set; }
    public decimal TotalDiscounts { get; set; }

    // Only set on a Z (closed) report.
    public decimal? ClosingCash { get; set; }
    public decimal? Variance { get; set; }
}
