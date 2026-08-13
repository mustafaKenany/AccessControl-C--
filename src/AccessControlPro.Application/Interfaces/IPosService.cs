using AccessControlPro.Application.DTOs;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Application.Interfaces;

public interface IPosService
{
    // Products
    Task<IEnumerable<ProductDto>> GetAllProductsAsync();
    Task AddProductAsync(ProductDto dto);
    Task UpdateProductAsync(ProductDto dto);
    Task DeleteProductAsync(int id);

    // Barcode lookup
    Task<ProductDto?> GetByBarcodeAsync(string barcode);

    // Sales. amountPaid: how much cash the customer hands over now; the remainder (if a player is
    // selected) becomes their POS debt. null = pay in full (Cash/CardBalance) or fully on credit (Credit).
    Task<bool> SellAsync(List<CartItemDto> items, PaymentMethod method, int? employeeId = null,
        decimal discountAmount = 0, string discountReason = "", decimal? amountPaid = null);

    // Card balance
    Task<decimal> GetCardBalanceAsync(int employeeId);
    Task TopUpCardAsync(int employeeId, decimal amount);

    // Player credit / debt (POS "on account" sales)
    Task<decimal> GetDebtAsync(int employeeId);
    /// <summary>Player pays down their POS debt. Records the cash as income and lowers the debt.</summary>
    Task CollectDebtAsync(int employeeId, decimal amount);
    /// <summary>Players who currently owe money from credit sales (Debt &gt; 0), with debt-aging info.</summary>
    Task<IEnumerable<EmployeeDto>> GetPlayersWithDebtAsync();

    // Feature 3 — refund tied to the original sale
    /// <summary>Recent sales (one per receipt) with per-line remaining-refundable quantities.</summary>
    Task<List<PosSaleDto>> GetRecentSalesAsync(int count = 30);
    /// <summary>Refund specific items from a receipt (capped at what remains, at the price paid, to the
    /// original instrument; cancels the sale's debt first). Returns false if nothing to refund.</summary>
    Task<bool> RefundSaleAsync(string receiptNo, List<CartItemDto> items, string reason = "");

    // Today's sales summary
    Task<(int Count, decimal Total)> GetTodaySalesAsync();

    // Daily summary (end of day report)
    Task<DailySummaryDto> GetDailySummaryAsync(DateTime date);

    // Shift management
    Task<PosShift?> GetOpenShiftAsync();
    Task<PosShift> OpenShiftAsync(decimal openingCash);
    Task<PosShift> CloseShiftAsync(decimal closingCash);
    /// <summary>Live "X" end-of-day report for the open shift (drawer breakdown; does NOT close it).
    /// Returns null if no shift is open.</summary>
    Task<ShiftReportDto?> GetShiftReportAsync();
}

public class CartItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total => (Price * Quantity) - DiscountAmount;
}

public class DailySummaryDto
{
    public DateTime Date { get; set; }
    public int TotalTransactions { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalCashSales { get; set; }
    public decimal TotalCardSales { get; set; }
    public decimal TotalDiscounts { get; set; }
    public int TotalItemsSold { get; set; }
    public List<CategorySummaryDto> ByCategory { get; set; } = new();
}

public class CategorySummaryDto
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Total { get; set; }
}
