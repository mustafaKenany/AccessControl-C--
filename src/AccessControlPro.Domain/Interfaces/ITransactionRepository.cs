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
    /// <summary>Find a transaction by its source-document reference (e.g. "PO-12"). Returns the most recent match.</summary>
    Task<Transaction?> GetByReferenceAsync(string reference);
    Task AddAsync(Transaction transaction);
    Task UpdateAsync(Transaction transaction);
    Task DeleteAsync(int id);
    Task<decimal> GetTotalByTypeAsync(TransactionType type, DateTime? from = null, DateTime? to = null);
    Task<IEnumerable<Transaction>> GetRecentAsync(int count);
    Task<IEnumerable<Transaction>> GetByEmployeeIdAsync(int employeeId);
    Task NullifyEmployeeIdAsync(int employeeId);
}
