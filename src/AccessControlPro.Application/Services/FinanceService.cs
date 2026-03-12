using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class FinanceService : IFinanceService
{
    private readonly ITransactionRepository _transactionRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly CurrentUserService _currentUser;

    public FinanceService(
        ITransactionRepository transactionRepo,
        IEmployeeRepository employeeRepo,
        CurrentUserService currentUser)
    {
        _transactionRepo = transactionRepo;
        _employeeRepo = employeeRepo;
        _currentUser = currentUser;
    }

    public async Task<FinanceSummaryDto> GetSummaryAsync(DateTime? from = null, DateTime? to = null, string? search = null)
    {
        var totalRevenue = await _transactionRepo.GetTotalByTypeAsync(TransactionType.Income, from, to);
        var totalExpenses = await _transactionRepo.GetTotalByTypeAsync(TransactionType.Expense, from, to);

        // Use paged query for recent transactions (supports date + search filtering)
        var (recentItems, _) = await _transactionRepo.GetPagedAsync(1, 20, null, from, to, search);

        // Outstanding balances: players where AmountPaid < SubscriptionFee
        var allPlayers = await _employeeRepo.GetAllWithCardsAsync();
        var outstanding = allPlayers
            .Where(e => e.SubscriptionFee > e.AmountPaid)
            .Select(e => new OutstandingPlayerDto
            {
                Id = e.Id,
                NameEn = e.FullNameEn,
                NameAr = e.FullNameAr,
                CardNo = e.CardNo,
                Fee = e.SubscriptionFee,
                Paid = e.AmountPaid
            }).ToList();

        // Filter outstanding players by search text
        if (!string.IsNullOrWhiteSpace(search))
        {
            outstanding = outstanding
                .Where(o => o.NameEn.Contains(search, StringComparison.OrdinalIgnoreCase)
                         || o.NameAr.Contains(search, StringComparison.OrdinalIgnoreCase)
                         || o.CardNo.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return new FinanceSummaryDto
        {
            TotalRevenue = totalRevenue,
            TotalExpenses = totalExpenses,
            NetProfit = Math.Round(totalRevenue - totalExpenses, 2),
            UnpaidBalances = outstanding.Sum(o => o.Remaining),
            RecentTransactions = recentItems.Select(ToDto).ToList(),
            OutstandingPlayers = outstanding
        };
    }

    public async Task PayOutstandingAsync(int employeeId, decimal amount)
    {
        var employee = await _employeeRepo.GetByIdWithCardsAsync(employeeId);
        if (employee == null) throw new InvalidOperationException("Employee not found.");
        if (amount <= 0) throw new ArgumentException("Payment amount must be greater than zero.");

        employee.AmountPaid += amount;
        await _employeeRepo.UpdateAsync(employee);

        // Record as income transaction
        await _transactionRepo.AddAsync(new Transaction
        {
            Type = TransactionType.Income,
            Category = "Subscription",
            Amount = amount,
            Description = $"Outstanding payment from {employee.FullNameEn}",
            RelatedEmployeeId = employeeId,
            PaymentMethod = PaymentMethod.Cash,
            CreatedBy = _currentUser.Username ?? "System"
        });
    }

    private static TransactionDto ToDto(Transaction t) => new()
    {
        Id = t.Id,
        Type = t.Type,
        Category = t.Category,
        Amount = t.Amount,
        Description = t.Description,
        RelatedEmployeeId = t.RelatedEmployeeId,
        RelatedEmployeeName = t.RelatedEmployee?.FullNameEn,
        PaymentMethod = t.PaymentMethod,
        CreatedBy = t.CreatedBy,
        CreatedAt = t.CreatedAt
    };
}
