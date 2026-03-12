using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class FreezeHistoryRepository : IFreezeHistoryRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public FreezeHistoryRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task AddAsync(FreezeHistory entity)
    {
        await using var db = _factory.CreateDbContext();
        db.FreezeHistories.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(FreezeHistory entity)
    {
        await using var db = _factory.CreateDbContext();
        db.FreezeHistories.Update(entity);
        await db.SaveChangesAsync();
    }

    public async Task<FreezeHistory?> GetActiveFreezeAsync(int employeeId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.FreezeHistories
            .Where(f => f.EmployeeId == employeeId && f.FreezeEnd == null)
            .OrderByDescending(f => f.FreezeStart)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<FreezeHistory>> GetByEmployeeIdAsync(int employeeId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.FreezeHistories
            .Where(f => f.EmployeeId == employeeId)
            .OrderByDescending(f => f.FreezeStart)
            .ToListAsync();
    }
}
