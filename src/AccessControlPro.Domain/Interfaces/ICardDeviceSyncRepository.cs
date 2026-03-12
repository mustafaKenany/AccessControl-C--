using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface ICardDeviceSyncRepository
{
    Task<CardDeviceSync?> GetAsync(int cardId, int deviceId);
    Task<IEnumerable<CardDeviceSync>> GetByCardIdAsync(int cardId);
    Task<IEnumerable<CardDeviceSync>> GetByDeviceIdAsync(int deviceId);
    Task<IEnumerable<CardDeviceSync>> GetFailedSyncsAsync();
    Task UpsertAsync(int cardId, int deviceId, bool isSynced, string? error = null);
    Task DeleteByCardIdAsync(int cardId);
    Task DeleteByDeviceIdAsync(int deviceId);
}
