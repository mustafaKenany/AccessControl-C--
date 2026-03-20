using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class QrPassRepository : IQrPassRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public QrPassRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<QrPass?> GetByIdAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.QrPasses.FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<QrPass?> GetByPassCodeAsync(string passCode)
    {
        await using var db = _factory.CreateDbContext();
        return await db.QrPasses.FirstOrDefaultAsync(q => q.PassCode == passCode);
    }

    public async Task<(IEnumerable<QrPass> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search = null, bool? activeOnly = null)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.QrPasses.AsQueryable();

        if (activeOnly == true)
            query = query.Where(q => q.IsActive && q.ValidTo >= DateTime.UtcNow && q.UsedCount < q.MaxUses);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(q => q.PlayerName.Contains(search) ||
                                     q.Phone.Contains(search) ||
                                     q.PassCode.Contains(search));

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task AddAsync(QrPass qrPass)
    {
        await using var db = _factory.CreateDbContext();
        db.QrPasses.Add(qrPass);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(QrPass qrPass)
    {
        await using var db = _factory.CreateDbContext();
        db.QrPasses.Update(qrPass);
        await db.SaveChangesAsync();
    }

    public async Task<int> GetActiveTodayCountAsync()
    {
        await using var db = _factory.CreateDbContext();
        var now = DateTime.UtcNow;
        return await db.QrPasses.CountAsync(q => q.IsActive && q.ValidFrom <= now && q.ValidTo >= now);
    }

    public async Task<IEnumerable<QrPass>> GetExpiredActivePassesAsync()
    {
        await using var db = _factory.CreateDbContext();
        var now = DateTime.UtcNow;
        return await db.QrPasses
            .Where(q => q.IsActive && (q.ValidTo < now || q.UsedCount >= q.MaxUses))
            .ToListAsync();
    }
}
