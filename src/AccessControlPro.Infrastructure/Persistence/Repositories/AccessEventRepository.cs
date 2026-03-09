using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class AccessEventRepository : IAccessEventRepository
{
    private readonly AppDbContext _context;

    public AccessEventRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<AccessEvent>> GetRecentAsync(int count)
        => await _context.AccessEvents
            .Include(e => e.Door)
            .Include(e => e.Card)
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToListAsync();

    public async Task<IEnumerable<AccessEvent>> GetByDoorIdAsync(int doorId)
        => await _context.AccessEvents
            .Where(e => e.DoorId == doorId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync();

    public async Task<IEnumerable<AccessEvent>> GetByDateRangeAsync(DateTime from, DateTime to)
        => await _context.AccessEvents
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync();

    public async Task AddAsync(AccessEvent accessEvent)
    {
        _context.AccessEvents.Add(accessEvent);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetTodayCountAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _context.AccessEvents.CountAsync(e => e.Timestamp >= today);
    }

    public async Task<int> GetActiveAlarmCountAsync()
        => await _context.AccessEvents
            .CountAsync(e => e.EventType == RecordType.Alarm && e.Timestamp >= DateTime.UtcNow.Date);
}
