using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class TimeGroupRepository : ITimeGroupRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public TimeGroupRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<TimeGroup>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.TimeGroups.OrderBy(t => t.HardwareIndex).ToListAsync();
    }

    public async Task<TimeGroup?> GetByIdAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.TimeGroups.FindAsync(id);
    }

    public async Task<TimeGroup?> GetByHardwareIndexAsync(int hardwareIndex)
    {
        await using var db = _factory.CreateDbContext();
        return await db.TimeGroups.FirstOrDefaultAsync(t => t.HardwareIndex == hardwareIndex);
    }

    public async Task<int> GetNextHardwareIndexAsync()
    {
        await using var db = _factory.CreateDbContext();
        var usedIndexes = await db.TimeGroups.Select(t => t.HardwareIndex).ToListAsync();
        for (int i = 1; i <= 64; i++)
        {
            if (!usedIndexes.Contains(i))
                return i;
        }
        return -1; // All 64 slots used
    }

    public async Task AddAsync(TimeGroup timeGroup)
    {
        await using var db = _factory.CreateDbContext();
        db.TimeGroups.Add(timeGroup);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(TimeGroup timeGroup)
    {
        await using var db = _factory.CreateDbContext();
        var existing = await db.TimeGroups.FindAsync(timeGroup.Id);
        if (existing == null) return;

        existing.NameEn = timeGroup.NameEn;
        existing.NameAr = timeGroup.NameAr;
        existing.ScheduleJson = timeGroup.ScheduleJson;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        var item = await db.TimeGroups.FindAsync(id);
        if (item != null)
        {
            db.TimeGroups.Remove(item);
            await db.SaveChangesAsync();
        }
    }
}
