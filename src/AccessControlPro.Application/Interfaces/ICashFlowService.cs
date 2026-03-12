using AccessControlPro.Application.DTOs;
using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Application.Interfaces;

public interface ICashFlowService
{
    Task<(IEnumerable<TransactionDto> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, TransactionType? type = null,
        DateTime? from = null, DateTime? to = null, string? search = null,
        string? category = null);
    Task AddExpenseAsync(string category, decimal amount, string description);
    Task AddIncomeAsync(string category, decimal amount, string description, int? employeeId = null, PaymentMethod method = PaymentMethod.Cash);
}
