using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface ITimeGroupRepository
{
    Task<IEnumerable<TimeGroup>> GetAllAsync();
    Task<TimeGroup?> GetByIdAsync(int id);
    Task<TimeGroup?> GetByHardwareIndexAsync(int hardwareIndex);
    Task<int> GetNextHardwareIndexAsync();
    Task AddAsync(TimeGroup timeGroup);
    Task UpdateAsync(TimeGroup timeGroup);
    Task DeleteAsync(int id);
}
