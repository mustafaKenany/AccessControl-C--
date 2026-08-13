namespace AccessControlPro.Application.DTOs;

/// <summary>Aggregated sales analytics for the Admin sales dashboard. Ported from the HikVision fork.</summary>
public class SalesDashboardDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    public decimal TotalRevenue { get; set; }
    public decimal TotalProfit { get; set; }
    public int SalesCount { get; set; }
    public int UnitsSold { get; set; }

    public List<ProductSalesReportItemDto> TopSellers { get; set; } = new();   // by revenue, desc
    public List<ProductSalesReportItemDto> SlowMovers { get; set; } = new();   // sold at least 1, asc

    /// <summary>Revenue + profit per calendar month, oldest→newest, for the trend chart.</summary>
    public List<MonthlySalesPointDto> MonthlyTrend { get; set; } = new();
}

public class MonthlySalesPointDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = string.Empty;   // e.g. "2026-08"
    public decimal Revenue { get; set; }
    public decimal Profit { get; set; }
}
