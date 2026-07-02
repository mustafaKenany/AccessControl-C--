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
        // Parallel fetch — all 4 queries are independent (each gets its own DbContext)
        var revenueTask = _transactionRepo.GetTotalByTypeAsync(TransactionType.Income, from, to);
        var expensesTask = _transactionRepo.GetTotalByTypeAsync(TransactionType.Expense, from, to);
        var recentTask = _transactionRepo.GetPagedAsync(1, 20, null, from, to, search);
        // Outstanding: search + cap the DISPLAY list in the DB (was: load ALL then filter in C#);
        // the correct TOTAL comes from a separate DB SUM so capping never skews it.
        var playersTask = _employeeRepo.GetOutstandingBalancesAsync(search);
        var unpaidSumTask = _employeeRepo.GetTotalOutstandingAsync();

        await Task.WhenAll(revenueTask, expensesTask, recentTask, playersTask, unpaidSumTask);

        var totalRevenue = await revenueTask;
        var totalExpenses = await expensesTask;
        var (recentItems, _) = await recentTask;
        var outstandingPlayers = await playersTask;
        var outstanding = outstandingPlayers
            .Select(e => new OutstandingPlayerDto
            {
                Id = e.Id,
                NameEn = e.FullNameEn,
                NameAr = e.FullNameAr,
                CardNo = e.CardNo,
                Fee = e.SubscriptionFee,
                Paid = e.AmountPaid
            }).ToList();

        return new FinanceSummaryDto
        {
            TotalRevenue = totalRevenue,
            TotalExpenses = totalExpenses,
            NetProfit = Math.Round(totalRevenue - totalExpenses, 2),
            UnpaidBalances = await unpaidSumTask,
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

    public async Task<List<TransactionDto>> GetTransactionsAsync(
        TransactionType? type = null, DateTime? from = null, DateTime? to = null, string? search = null)
    {
        // pageSize large enough to return the full filtered set for a printable report
        var (items, _) = await _transactionRepo.GetPagedAsync(1, 100000, type, from, to, search);
        return items.Select(ToDto).ToList();
    }

    public async Task RecordIncomeAsync(string category, decimal amount, string description)
    {
        if (amount <= 0) return;
        await _transactionRepo.AddAsync(new Transaction
        {
            Type = TransactionType.Income,
            Category = category,
            Amount = amount,
            Description = description,
            PaymentMethod = PaymentMethod.Cash,
            CreatedBy = _currentUser.Username ?? "System",
            CreatedAt = DateTime.UtcNow
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
