using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IEmployeeRepository
{
    Task<IEnumerable<Employee>> GetAllWithCardsAsync();
    Task<(IEnumerable<Employee> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null);
    /// <summary>Count of members (matching the optional search) that have at least one card record —
    /// for the full-dataset "has card / no card" badges, independent of the current page.</summary>
    Task<int> GetWithCardCountAsync(string? search = null);

    /// <summary>DB-level paged load for a filter pill (1 Expiring / 2 Renewed / 3 Frozen / 4 Expired /
    /// 5 Active). Returns the page, total count, and has-card count so filters don't load all at once.</summary>
    Task<(IEnumerable<Employee> Items, int TotalCount, int WithCardCount)> GetFilteredPagedAsync(
        int filter, DateTime from, DateTime to, int page, int pageSize, string? search = null);
    Task<Employee?> GetByIdWithCardsAsync(int id);
    Task<Employee?> GetByEmployeeCodeAsync(string cardNo);
    Task<Employee?> GetByPhoneAsync(string phone);
    Task<bool> ExistsByNameAsync(string fullNameEn, int? excludeId = null);
    Task AddAsync(Employee employee);
    Task UpdateAsync(Employee employee);
    Task DeleteAsync(int id);
    Task<int> GetCountAsync();
    Task<IEnumerable<Employee>> GetBySubscriptionEndDateRangeAsync(DateTime from, DateTime to);
    Task<IEnumerable<Employee>> GetByStartDateRangeAsync(DateTime from, DateTime to);
    Task<IEnumerable<Employee>> GetFrozenAsync();
    Task<IEnumerable<Employee>> GetExpiredAsync();
    Task<IEnumerable<Employee>> GetActiveAsync();
    Task<IEnumerable<Employee>> GetOutstandingBalancesAsync();
    /// <summary>Lightweight: returns only Id, CardNo, StartDate, EndDate, MaxVisits (no photos)</summary>
    Task<IEnumerable<(int Id, string CardNo, DateTime StartDate, DateTime EndDate, int MaxVisits)>> GetCardInfoForSyncAsync();
}
