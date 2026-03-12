using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class CashFlowService : ICashFlowService
{
    private readonly ITransactionRepository _transactionRepo;
    private readonly CurrentUserService _currentUser;

    public CashFlowService(ITransactionRepository transactionRepo, CurrentUserService currentUser)
    {
        _transactionRepo = transactionRepo;
        _currentUser = currentUser;
    }

    public async Task<(IEnumerable<TransactionDto> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, TransactionType? type = null,
        DateTime? from = null, DateTime? to = null, string? search = null,
        string? category = null)
    {
        var (items, totalCount) = await _transactionRepo.GetPagedAsync(page, pageSize, type, from, to, search, category);
        return (items.Select(ToDto), totalCount);
    }

    public async Task AddExpenseAsync(string category, decimal amount, string description)
    {
        await _transactionRepo.AddAsync(new Transaction
        {
            Type = TransactionType.Expense,
            Category = category,
            Amount = amount,
            Description = description,
            PaymentMethod = PaymentMethod.Cash,
            CreatedBy = _currentUser.Username ?? "System"
        });
    }

    public async Task AddIncomeAsync(string category, decimal amount, string description, int? employeeId = null, PaymentMethod method = PaymentMethod.Cash)
    {
        await _transactionRepo.AddAsync(new Transaction
        {
            Type = TransactionType.Income,
            Category = category,
            Amount = amount,
            Description = description,
            RelatedEmployeeId = employeeId,
            PaymentMethod = method,
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
