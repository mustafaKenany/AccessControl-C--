using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class DoorRepository : IDoorRepository
{
    private readonly AppDbContext _context;

    public DoorRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Door>> GetAllWithDeviceAsync()
        => await _context.Doors.Include(d => d.Device).OrderBy(d => d.DeviceId).ThenBy(d => d.DoorNumber).ToListAsync();

    public async Task<Door?> GetByIdWithDeviceAsync(int id)
        => await _context.Doors.Include(d => d.Device).FirstOrDefaultAsync(d => d.Id == id);

    public async Task<IEnumerable<Door>> GetByDeviceIdAsync(int deviceId)
        => await _context.Doors.Where(d => d.DeviceId == deviceId).OrderBy(d => d.DoorNumber).ToListAsync();

    public async Task AddRangeAsync(IEnumerable<Door> doors)
    {
        _context.Doors.AddRange(doors);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Door door)
    {
        // Don't call Update() - entity is already tracked by the context.
        // Update() would also mark navigation properties (Device) as Modified,
        // causing EF Core to generate an unnecessary UPDATE for Device too.
        await _context.SaveChangesAsync();
    }
}
