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
            // SQL Server's default collation is case-insensitive — drop the ToLower() (it forced a
            // per-row computed scan and helped nothing). The date range above is the primary filter.
            var s = search.Trim();
            query = query.Where(l =>
                EF.Functions.Like(l.Action, "%" + s + "%") ||
                EF.Functions.Like(l.EntityType, "%" + s + "%") ||
                EF.Functions.Like(l.Details, "%" + s + "%") ||
                EF.Functions.Like(l.DetailsAr, "%" + s + "%") ||
                EF.Functions.Like(l.PerformedBy, "%" + s + "%"));
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
