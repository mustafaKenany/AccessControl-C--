using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IDoorRepository
{
    Task<IEnumerable<Door>> GetAllWithDeviceAsync();
    Task<Door?> GetByIdWithDeviceAsync(int id);
    Task<IEnumerable<Door>> GetByDeviceIdAsync(int deviceId);
    Task AddRangeAsync(IEnumerable<Door> doors);
    Task UpdateAsync(Door door);
    Task<IEnumerable<Door>> GetByDeviceSerialAsync(string serialNumber);
}
