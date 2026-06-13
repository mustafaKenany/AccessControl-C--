using AccessControlPro.Application.DTOs;

namespace AccessControlPro.Application.Interfaces;

public class FinanceSummaryDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal UnpaidBalances { get; set; }
    public List<TransactionDto> RecentTransactions { get; set; } = new();
    public List<OutstandingPlayerDto> OutstandingPlayers { get; set; } = new();
}

public class OutstandingPlayerDto
{
    public int Id { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string CardNo { get; set; } = string.Empty;
    public decimal Fee { get; set; }
    public decimal Paid { get; set; }
    public decimal Remaining => Fee - Paid;
}

public interface IFinanceService
{
    Task<FinanceSummaryDto> GetSummaryAsync(DateTime? from = null, DateTime? to = null, string? search = null);
    Task PayOutstandingAsync(int employeeId, decimal amount);

    /// <summary>Full (non-paged) transaction list for a period/type — used for printable reports.</summary>
    Task<List<TransactionDto>> GetTransactionsAsync(
        Domain.Enums.TransactionType? type = null,
        DateTime? from = null, DateTime? to = null, string? search = null);

    /// <summary>Record a one-off income transaction (e.g. a daily-pass temporary card).</summary>
    Task RecordIncomeAsync(string category, decimal amount, string description);
}
