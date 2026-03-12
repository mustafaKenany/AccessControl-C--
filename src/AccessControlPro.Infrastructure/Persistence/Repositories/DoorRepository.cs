using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class DoorRepository : IDoorRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DoorRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<Door>> GetAllWithDeviceAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Doors.Include(d => d.Device).OrderBy(d => d.DeviceId).ThenBy(d => d.DoorNumber).ToListAsync();
    }

    public async Task<Door?> GetByIdWithDeviceAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Doors.Include(d => d.Device).FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<IEnumerable<Door>> GetByDeviceIdAsync(int deviceId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Doors.Where(d => d.DeviceId == deviceId).OrderBy(d => d.DoorNumber).ToListAsync();
    }

    public async Task AddRangeAsync(IEnumerable<Door> doors)
    {
        await using var db = _factory.CreateDbContext();
        db.Doors.AddRange(doors);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Door door)
    {
        await using var db = _factory.CreateDbContext();
        db.Doors.Update(door);
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<Door>> GetByDeviceSerialAsync(string serialNumber)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Doors.Include(d => d.Device)
            .Where(d => d.Device != null && d.Device.SerialNumber == serialNumber)
            .OrderBy(d => d.DoorNumber)
            .ToListAsync();
    }
}
