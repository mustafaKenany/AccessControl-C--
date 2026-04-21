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
            // Same PC can always re-acquire (user stopped and started again quickly)
            if (string.Equals(existing.MachineName, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                existing.AcquiredAt = DateTime.UtcNow;
                existing.HeartbeatAt = DateTime.UtcNow;
                existing.UserName = Environment.UserName;
                await db.SaveChangesAsync();
                return (true, null);
            }

            // Different PC - check if heartbeat is recent (lock is active)
            if (DateTime.UtcNow - existing.HeartbeatAt < StaleThreshold)
            {
                return (false, $"{existing.MachineName} ({existing.UserName})");
            }

            // Stale lock from another PC — take over
            existing.MachineName = Environment.MachineName;
            existing.UserName = Environment.UserName;
            existing.AcquiredAt = DateTime.UtcNow;
            existing.HeartbeatAt = DateTime.UtcNow;
        }
        else
        {
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
        try
        {
            await using var db = _factory.CreateDbContext();
            // Remove ALL locks from this machine (not just one)
            var myLocks = await db.MonitorLocks
                .Where(l => l.MachineName == Environment.MachineName)
                .ToListAsync();
            if (myLocks.Count > 0)
            {
                db.MonitorLocks.RemoveRange(myLocks);
                await db.SaveChangesAsync();
            }
        }
        catch { /* Don't crash if DB fails during release */ }
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
