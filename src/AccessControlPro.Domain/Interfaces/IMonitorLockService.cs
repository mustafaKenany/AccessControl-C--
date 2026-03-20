namespace AccessControlPro.Domain.Interfaces;

public interface IMonitorLockService
{
    /// <summary>
    /// Try to acquire the monitor lock. Returns (success, errorMessage).
    /// If another PC holds the lock with recent heartbeat, returns false + who holds it.
    /// </summary>
    Task<(bool acquired, string? holder)> TryAcquireAsync();

    /// <summary>Release the lock when monitor stops.</summary>
    Task ReleaseAsync();

    /// <summary>Send heartbeat to keep the lock alive (call every 10s).</summary>
    Task HeartbeatAsync();
}
