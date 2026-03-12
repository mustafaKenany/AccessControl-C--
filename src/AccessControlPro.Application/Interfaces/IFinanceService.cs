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
}
