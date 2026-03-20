using AccessControlPro.Domain.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence;

public class MonitorLockService : IMonitorLockService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(30);

    public MonitorLockService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<(bool acquired, string? holder)> TryAcquireAsync()
    {
        await using var db = _factory.CreateDbContext();
        var existing = await db.MonitorLocks.FirstOrDefaultAsync();

        if (existing != null)
        {
            // Check if heartbeat is recent (lock is active)
            if (DateTime.UtcNow - existing.HeartbeatAt < StaleThreshold)
            {
                // Lock is held by another PC
                return (false, $"{existing.MachineName} ({existing.UserName})");
            }

            // Stale lock — take over
            existing.MachineName = Environment.MachineName;
            existing.UserName = Environment.UserName;
            existing.AcquiredAt = DateTime.UtcNow;
            existing.HeartbeatAt = DateTime.UtcNow;
        }
        else
        {
            // No lock exists — create one
            db.MonitorLocks.Add(new MonitorLock
            {
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                AcquiredAt = DateTime.UtcNow,
                HeartbeatAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task ReleaseAsync()
    {
        await using var db = _factory.CreateDbContext();
        var mine = await db.MonitorLocks
            .FirstOrDefaultAsync(l => l.MachineName == Environment.MachineName);
        if (mine != null)
        {
            db.MonitorLocks.Remove(mine);
            await db.SaveChangesAsync();
        }
    }

    public async Task HeartbeatAsync()
    {
        await using var db = _factory.CreateDbContext();
        var mine = await db.MonitorLocks
            .FirstOrDefaultAsync(l => l.MachineName == Environment.MachineName);
        if (mine != null)
        {
            mine.HeartbeatAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }
}
