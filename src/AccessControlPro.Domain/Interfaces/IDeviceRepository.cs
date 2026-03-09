using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IDeviceRepository
{
    Task<IEnumerable<Device>> GetAllAsync();
    Task<Device?> GetByIdAsync(int id);
    Task<Device?> GetBySerialNumberAsync(string serialNumber);
    Task AddAsync(Device device);
    Task UpdateAsync(Device device);
    Task DeleteAsync(int id);
    Task<int> GetOnlineCountAsync();
}
