using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

/// <summary>Lightweight, PHOTO-FREE row for the 10-second expiry monitor. Loading full players WITH
/// their JPEG PhotoData every 10s was the 32-bit OOM leak (~108 MB/min of dead photo byte[]).</summary>
public record ExpiryMonitorRow(int Id, string FullNameEn, bool IsFrozen, DateTime EndDate,
    int MaxVisits, int UsedVisits, bool HasActiveSyncedCard);

public interface IEmployeeRepository
{
    /// <summary>Photo-free, AsNoTracking projection of every player's expiry-relevant fields, for the
    /// frequent (10s) expiry monitor. NEVER load PhotoData on this hot path — that was the OOM leak.</summary>
    Task<IReadOnlyList<ExpiryMonitorRow>> GetExpiryMonitorRowsAsync();

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
    Task<IEnumerable<Employee>> GetOutstandingBalancesAsync(string? search = null, int take = 500);
    /// <summary>DB-side SUM of net owed across ALL outstanding members (no rows loaded).</summary>
    Task<decimal> GetTotalOutstandingAsync();
    /// <summary>Lightweight: returns only Id, CardNo, StartDate, EndDate, MaxVisits (no photos)</summary>
    Task<IEnumerable<(int Id, string CardNo, DateTime StartDate, DateTime EndDate, int MaxVisits)>> GetCardInfoForSyncAsync();
}
