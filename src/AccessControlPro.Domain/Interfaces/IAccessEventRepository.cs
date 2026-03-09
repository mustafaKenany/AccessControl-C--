using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IAccessEventRepository
{
    Task<IEnumerable<AccessEvent>> GetRecentAsync(int count);
    Task<IEnumerable<AccessEvent>> GetByDoorIdAsync(int doorId);
    Task<IEnumerable<AccessEvent>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task AddAsync(AccessEvent accessEvent);
    Task<int> GetTodayCountAsync();
    Task<int> GetActiveAlarmCountAsync();
}
