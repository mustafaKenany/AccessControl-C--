using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Domain.Interfaces;

public interface ITransactionRepository
{
    Task<(IEnumerable<Transaction> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, TransactionType? type = null,
        DateTime? from = null, DateTime? to = null, string? search = null,
        string? category = null);
    Task<Transaction?> GetByIdAsync(int id);
    Task AddAsync(Transaction transaction);
    Task<decimal> GetTotalByTypeAsync(TransactionType type, DateTime? from = null, DateTime? to = null);
    Task<IEnumerable<Transaction>> GetRecentAsync(int count);
    Task<IEnumerable<Transaction>> GetByEmployeeIdAsync(int employeeId);
}
