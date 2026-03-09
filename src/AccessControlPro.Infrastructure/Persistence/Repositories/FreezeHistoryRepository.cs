using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class FreezeHistoryRepository : IFreezeHistoryRepository
{
    private readonly AppDbContext _context;

    public FreezeHistoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(FreezeHistory entity)
    {
        _context.FreezeHistories.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(FreezeHistory entity)
    {
        _context.FreezeHistories.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<FreezeHistory?> GetActiveFreezeAsync(int employeeId)
    {
        return await _context.FreezeHistories
            .Where(f => f.EmployeeId == employeeId && f.FreezeEnd == null)
            .OrderByDescending(f => f.FreezeStart)
            .FirstOrDefaultAsync();
    }
}
