using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class PosShiftRepository : IPosShiftRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PosShiftRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<PosShift?> GetOpenShiftAsync()
    {
        using var db = _factory.CreateDbContext();
        return await db.PosShifts
            .Where(s => s.Status == "Open")
            .OrderByDescending(s => s.OpenedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<PosShift>> GetAllOpenShiftsAsync()
    {
        using var db = _factory.CreateDbContext();
        return await db.PosShifts
            .Where(s => s.Status == "Open")
            .ToListAsync();
    }

    public async Task AddAsync(PosShift shift)
    {
        using var db = _factory.CreateDbContext();
        db.PosShifts.Add(shift);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(PosShift shift)
    {
        using var db = _factory.CreateDbContext();
        db.PosShifts.Attach(shift);
        db.Entry(shift).State = EntityState.Modified;
        await db.SaveChangesAsync();
    }

    public async Task UpdateRangeAsync(IEnumerable<PosShift> shifts)
    {
        using var db = _factory.CreateDbContext();
        foreach (var shift in shifts)
        {
            db.PosShifts.Attach(shift);
            db.Entry(shift).State = EntityState.Modified;
        }
        await db.SaveChangesAsync();
    }
}
