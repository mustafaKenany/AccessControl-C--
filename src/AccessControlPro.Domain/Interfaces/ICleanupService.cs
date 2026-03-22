namespace AccessControlPro.Domain.Interfaces;

public interface ICleanupService
{
    /// <summary>
    /// Archives and deletes players whose subscription expired more than 6 months ago.
    /// Handles FK constraints by nullifying/deleting related records first.
    /// </summary>
    Task<int> CleanupInactivePlayersAsync();

    /// <summary>
    /// Deletes access events older than 6 months and audit logs older than 1 year.
    /// </summary>
    Task<int> CleanupOldEventsAsync();
}
