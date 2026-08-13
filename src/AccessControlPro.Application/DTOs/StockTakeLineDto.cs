namespace AccessControlPro.Application.DTOs;

/// <summary>One product line in a physical stock-take: the system count vs the counted count.
/// Ported from the HikVision fork.</summary>
public class StockTakeLineDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Cost { get; set; }

    public int SystemStock { get; set; }
    /// <summary>What the operator physically counted. Starts equal to SystemStock (edit to change).</summary>
    public int CountedStock { get; set; }

    public int Variance => CountedStock - SystemStock;   // + = found more, − = shrinkage
}

/// <summary>Result of applying a stock-take.</summary>
public class StockTakeResultDto
{
    public int LinesAdjusted { get; set; }
    public int TotalShortageUnits { get; set; }     // sum of negative variances (units lost)
    public int TotalOverageUnits { get; set; }      // sum of positive variances
    public decimal ShortageValueAtCost { get; set; }
}
