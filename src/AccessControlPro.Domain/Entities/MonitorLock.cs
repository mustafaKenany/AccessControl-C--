namespace AccessControlPro.Domain.Entities;

/// <summary>
/// Database-level lock ensuring only ONE Real-Time Monitor runs across all PCs.
/// Single row table — whoever holds the lock sends heartbeats every 10s.
/// If heartbeat is stale (>30s), the lock is considered abandoned.
/// </summary>
public class MonitorLock
{
    public int Id { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime AcquiredAt { get; set; }
    public DateTime HeartbeatAt { get; set; }
}
