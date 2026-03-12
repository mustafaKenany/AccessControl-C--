using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DeviceRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<Device>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Devices.Include(d => d.Doors).ToListAsync();
    }

    public async Task<Device?> GetByIdAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Devices.Include(d => d.Doors).FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Device?> GetBySerialNumberAsync(string serialNumber)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Devices.FirstOrDefaultAsync(d => d.SerialNumber == serialNumber);
    }

    public async Task AddAsync(Device device)
    {
        await using var db = _factory.CreateDbContext();
        db.Devices.Add(device);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Device device)
    {
        await using var db = _factory.CreateDbContext();
        db.Devices.Update(device);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        var device = await db.Devices.FindAsync(id);
        if (device != null)
        {
            db.Devices.Remove(device);
            await db.SaveChangesAsync();
        }
    }

    public async Task<int> GetOnlineCountAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Devices.CountAsync(d => d.IsOnline);
    }
}
