namespace AccessControlPro.Domain.Interfaces;

/// <summary>
/// Interface for session-based operation logging
/// </summary>
public interface ISessionLogger
{
    /// <summary>
    /// Log an operation with bilingual descriptions
    /// </summary>
    Task LogOperationAsync(string operationType, string entityType, int? entityId,
        string descriptionEn, string descriptionAr, string? performedBy = null);

    /// <summary>
    /// Log a general message
    /// </summary>
    Task LogAsync(string message);

    /// <summary>
    /// Log an error
    /// </summary>
    Task LogErrorAsync(string operationType, string entityType, int? entityId,
        string errorMessage, string? performedBy = null);

    /// <summary>
    /// Clear session logs
    /// </summary>
    void ClearLogs();
}
