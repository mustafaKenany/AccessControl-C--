using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class LookupRepository : ILookupRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public LookupRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<List<LookupItem>> GetByCategoryAsync(string category)
    {
        await using var db = _factory.CreateDbContext();
        return await db.LookupItems
            .Where(x => x.Category == category && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<LookupItem?> GetByIdAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.LookupItems.FindAsync(id);
    }

    public async Task AddAsync(LookupItem item)
    {
        await using var db = _factory.CreateDbContext();
        db.LookupItems.Add(item);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(LookupItem item)
    {
        await using var db = _factory.CreateDbContext();
        var existing = await db.LookupItems.FindAsync(item.Id);
        if (existing == null) return;

        existing.Name = item.Name;
        existing.NameAr = item.NameAr;
        existing.NumericValue = item.NumericValue;
        existing.SortOrder = item.SortOrder;
        existing.IsActive = item.IsActive;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        var item = await db.LookupItems.FindAsync(id);
        if (item != null)
        {
            db.LookupItems.Remove(item);
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<SubscriptionPlan>> GetActiveSubscriptionPlansAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Id)
            .ToListAsync();
    }
}
