using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Domain.Interfaces;

public interface IAccessEventRepository
{
    Task<IEnumerable<AccessEvent>> GetRecentAsync(int count);
    Task<IEnumerable<AccessEvent>> GetByDoorIdAsync(int doorId);
    Task<IEnumerable<AccessEvent>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task AddAsync(AccessEvent accessEvent);
    Task<int> GetTodayCountAsync();
    Task<int> GetActiveAlarmCountAsync();
    Task<(IEnumerable<AccessEvent> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, DateTime? from = null, DateTime? to = null, int? doorId = null, string? search = null, RecordType? eventType = null, int? deviceId = null);
    Task<int> DeleteOlderThanAsync(DateTime cutoff);
    Task NullifyCardIdForCardsAsync(IEnumerable<int> cardIds);
}
