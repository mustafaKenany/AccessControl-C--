using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class AccessEventRepository : IAccessEventRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public AccessEventRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<AccessEvent>> GetRecentAsync(int count)
    {
        await using var db = _factory.CreateDbContext();
        return await db.AccessEvents
            .Include(e => e.Door)
            .Include(e => e.Card)
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<AccessEvent>> GetByDoorIdAsync(int doorId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.AccessEvents
            .Where(e => e.DoorId == doorId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<AccessEvent>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        await using var db = _factory.CreateDbContext();
        return await db.AccessEvents
            .Where(e => e.Timestamp >= from && e.Timestamp <= to)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync();
    }

    public async Task AddAsync(AccessEvent accessEvent)
    {
        await using var db = _factory.CreateDbContext();
        db.AccessEvents.Add(accessEvent);
        await db.SaveChangesAsync();
    }

    public async Task<int> GetTodayCountAsync()
    {
        await using var db = _factory.CreateDbContext();
        var today = DateTime.UtcNow.Date;
        return await db.AccessEvents.CountAsync(e => e.Timestamp >= today);
    }

    public async Task<int> GetActiveAlarmCountAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.AccessEvents
            .CountAsync(e => e.EventType == RecordType.Alarm && e.Timestamp >= DateTime.UtcNow.Date);
    }

    public async Task<(IEnumerable<AccessEvent> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, DateTime? from = null, DateTime? to = null, int? doorId = null, string? search = null,
        RecordType? eventType = null, int? deviceId = null)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.AccessEvents
            .Include(e => e.Door).ThenInclude(d => d.Device)
            .Include(e => e.Card).ThenInclude(c => c!.Employee)
            .AsQueryable();

        if (from.HasValue) query = query.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(e => e.Timestamp <= to.Value);
        if (doorId.HasValue) query = query.Where(e => e.DoorId == doorId.Value);
        if (eventType.HasValue) query = query.Where(e => e.EventType == eventType.Value);
        if (deviceId.HasValue) query = query.Where(e => e.Door != null && e.Door.Device != null && e.Door.Device.Id == deviceId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Strip leading zeros for card number matching (readers add leading zeros)
            var searchClean = search.TrimStart('0');
            query = query.Where(e =>
                (e.Card != null && e.Card.CardNumber.Contains(search)) ||
                (searchClean.Length > 0 && e.Card != null && e.Card.CardNumber.Contains(searchClean)) ||
                e.Details.Contains(search));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff)
    {
        await using var db = _factory.CreateDbContext();
        return await db.AccessEvents
            .Where(e => e.Timestamp < cutoff)
            .ExecuteDeleteAsync();
    }

    public async Task NullifyCardIdForCardsAsync(IEnumerable<int> cardIds)
    {
        var ids = cardIds.ToList();
        if (ids.Count == 0) return;
        await using var db = _factory.CreateDbContext();
        // Use parameterized query to prevent SQL injection
        var parameters = ids.Select((id, i) => new Microsoft.Data.SqlClient.SqlParameter($"@id{i}", id)).ToArray();
        var paramNames = string.Join(",", ids.Select((_, i) => $"@id{i}"));
        await db.Database.ExecuteSqlRawAsync(
            $"UPDATE AccessEvents SET CardId = NULL WHERE CardId IN ({paramNames})", parameters);
    }
}
