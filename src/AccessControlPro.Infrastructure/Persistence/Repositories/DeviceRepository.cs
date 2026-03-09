using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly AppDbContext _context;

    public DeviceRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Device>> GetAllAsync()
        => await _context.Devices.Include(d => d.Doors).ToListAsync();

    public async Task<Device?> GetByIdAsync(int id)
        => await _context.Devices.Include(d => d.Doors).FirstOrDefaultAsync(d => d.Id == id);

    public async Task<Device?> GetBySerialNumberAsync(string serialNumber)
        => await _context.Devices.FirstOrDefaultAsync(d => d.SerialNumber == serialNumber);

    public async Task AddAsync(Device device)
    {
        _context.Devices.Add(device);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Device device)
    {
        _context.Devices.Update(device);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device != null)
        {
            _context.Devices.Remove(device);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetOnlineCountAsync()
        => await _context.Devices.CountAsync(d => d.IsOnline);
}
