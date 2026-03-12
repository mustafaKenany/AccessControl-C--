using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AuditLogRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task AddAsync(AuditLog log)
    {
        await using var db = _factory.CreateDbContext();
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }

    public async Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null, DateTime? from = null, DateTime? to = null)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.AuditLogs.AsQueryable();

        if (from.HasValue)
            query = query.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.Timestamp <= to.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(l =>
                l.Action.ToLower().Contains(s) ||
                l.EntityType.ToLower().Contains(s) ||
                l.Details.ToLower().Contains(s) ||
                l.DetailsAr.ToLower().Contains(s) ||
                l.PerformedBy.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, int entityId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.AuditLogs
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .OrderByDescending(l => l.Timestamp)
            .Take(50)
            .ToListAsync();
    }
}
