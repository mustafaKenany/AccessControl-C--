using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class CardDeviceSyncRepository : ICardDeviceSyncRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public CardDeviceSyncRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<CardDeviceSync?> GetAsync(int cardId, int deviceId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.CardDeviceSyncs
            .FirstOrDefaultAsync(s => s.AccessCardId == cardId && s.DeviceId == deviceId);
    }

    public async Task<IEnumerable<CardDeviceSync>> GetByCardIdAsync(int cardId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.CardDeviceSyncs
            .Include(s => s.Device)
            .Where(s => s.AccessCardId == cardId)
            .ToListAsync();
    }

    public async Task<IEnumerable<CardDeviceSync>> GetByDeviceIdAsync(int deviceId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.CardDeviceSyncs
            .Include(s => s.AccessCard)
            .Where(s => s.DeviceId == deviceId)
            .ToListAsync();
    }

    public async Task<IEnumerable<CardDeviceSync>> GetFailedSyncsAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.CardDeviceSyncs
            .Include(s => s.AccessCard)
            .Include(s => s.Device)
            .Where(s => !s.IsSynced)
            .ToListAsync();
    }

    public async Task UpsertAsync(int cardId, int deviceId, bool isSynced, string? error = null)
    {
        await using var db = _factory.CreateDbContext();
        var existing = await db.CardDeviceSyncs
            .FirstOrDefaultAsync(s => s.AccessCardId == cardId && s.DeviceId == deviceId);

        if (existing != null)
        {
            existing.IsSynced = isSynced;
            existing.SyncedAt = isSynced ? DateTime.UtcNow : existing.SyncedAt;
            existing.LastError = error;
        }
        else
        {
            db.CardDeviceSyncs.Add(new CardDeviceSync
            {
                AccessCardId = cardId,
                DeviceId = deviceId,
                IsSynced = isSynced,
                SyncedAt = isSynced ? DateTime.UtcNow : null,
                LastError = error
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task DeleteByCardIdAsync(int cardId)
    {
        await using var db = _factory.CreateDbContext();
        var syncs = await db.CardDeviceSyncs.Where(s => s.AccessCardId == cardId).ToListAsync();
        db.CardDeviceSyncs.RemoveRange(syncs);
        await db.SaveChangesAsync();
    }

    public async Task DeleteByDeviceIdAsync(int deviceId)
    {
        await using var db = _factory.CreateDbContext();
        var syncs = await db.CardDeviceSyncs.Where(s => s.DeviceId == deviceId).ToListAsync();
        db.CardDeviceSyncs.RemoveRange(syncs);
        await db.SaveChangesAsync();
    }
}
