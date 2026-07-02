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
        // No Include — the projection below loads ONLY the displayed fields. The old Includes pulled
        // the full Card.Employee row (incl. the PhotoData blob) for every event = a big slowdown.
        var query = db.AccessEvents.AsQueryable();

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
            .Select(e => new AccessEvent
            {
                Id = e.Id, DoorId = e.DoorId, CardId = e.CardId, EventType = e.EventType,
                EventCode = e.EventCode, Timestamp = e.Timestamp, Details = e.Details,
                Door = e.Door == null ? null! : new Door
                {
                    Id = e.Door.Id, Name = e.Door.Name,
                    Device = e.Door.Device == null ? null! : new Device
                    {
                        Id = e.Door.Device.Id, Name = e.Door.Device.Name, SerialNumber = e.Door.Device.SerialNumber
                    }
                },
                Card = e.Card == null ? null : new AccessCard
                {
                    Id = e.Card.Id, CardNumber = e.Card.CardNumber,
                    Employee = e.Card.Employee == null ? null : new Employee
                    {
                        Id = e.Card.Employee.Id, FullNameEn = e.Card.Employee.FullNameEn, FullNameAr = e.Card.Employee.FullNameAr
                    }
                }
            })
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
