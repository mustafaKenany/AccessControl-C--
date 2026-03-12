namespace AccessControlPro.Domain.Entities;

/// <summary>
/// Tracks which cards are synced to which devices.
/// Enables multi-device sync status tracking and retry on failure.
/// </summary>
public class CardDeviceSync
{
    public int Id { get; set; }
    public int AccessCardId { get; set; }
    public int DeviceId { get; set; }
    public bool IsSynced { get; set; }
    public DateTime? SyncedAt { get; set; }
    public string? LastError { get; set; }

    public AccessCard AccessCard { get; set; } = null!;
    public Device Device { get; set; } = null!;
}
