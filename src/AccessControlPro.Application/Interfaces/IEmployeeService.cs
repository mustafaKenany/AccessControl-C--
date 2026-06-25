using AccessControlPro.Application.DTOs;

namespace AccessControlPro.Application.Interfaces;

/// <summary>Phased progress for repopulating a device: Phase is "Members" then "QrPool".</summary>
public record DeviceSyncProgress(string Phase, int Done, int Total);

public interface IEmployeeService
{
    Task<IEnumerable<EmployeeDto>> GetAllEmployeesAsync();
    Task<(IEnumerable<EmployeeDto> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null);
    Task<EmployeeDto?> GetEmployeeByIdAsync(int id);
    Task AddEmployeeAsync(EmployeeDto dto);
    Task<bool> UpdateEmployeeAsync(EmployeeDto dto, string? editReason = null);
    Task<bool> DeleteEmployeeAsync(int id);
    Task<bool> AssignCardAsync(int employeeId, AccessCardDto cardDto);
    Task<bool> RemoveCardAsync(int cardId);
    Task<bool> SyncCardToDeviceAsync(int cardId, int deviceId);
    /// <summary>Sync card to selected devices. Pass null for ALL devices.</summary>
    Task<(int synced, int failed, int total, List<string> errors)> SyncCardToDevicesAsync(int cardId, IEnumerable<int>? deviceIds = null);
    Task<(int synced, int failed, int total)> SyncAllCardsToDeviceAsync(int deviceId, IProgress<(int current, int total, string cardNumber)>? progress = null, bool activeSubscriptionsOnly = false);

    /// <summary>Counts for the "load data onto this device?" prompt: active-subscription members + pool codes.</summary>
    Task<(int activeMembers, int poolCodes)> GetDeviceSyncCountsAsync();

    /// <summary>Repopulate a device: push active-subscription members FIRST (gym usable within seconds),
    /// then the full QR pool (daily-pass + visitor) in the background. Reports phased progress.</summary>
    Task<(int membersSynced, int membersFailed, int poolPushed, int poolFailed)> SyncAllDataToDeviceAsync(
        int deviceId, IProgress<DeviceSyncProgress>? progress = null, System.Threading.CancellationToken ct = default);
    /// <summary>Sync all cards to selected devices. Pass null for ALL devices.</summary>
    Task<(int synced, int failed, int total)> SyncAllCardsToDevicesAsync(IEnumerable<int>? deviceIds = null, IProgress<(int current, int total, string cardNumber)>? progress = null);
    /// <summary>Remove/expire card on selected devices. Pass null for ALL devices.</summary>
    Task<(int synced, int failed, int total, List<string> errors)> RemoveCardFromDevicesAsync(int cardId, IEnumerable<int>? deviceIds = null);

    /// <summary>
    /// Push a raw card number straight to the gate(s) valid until <paramref name="validTo"/>,
    /// so the controller itself rejects it afterwards (date-enforced expiry). Used by the
    /// anonymous Daily Pass — no AccessCard/player record is created. Pass null for ALL devices.
    /// </summary>
    Task<(int ok, int fail, int total, List<string> errors)> PushTempCardToDevicesAsync(
        string cardNumber, DateTime validTo, string doorPermissions, IEnumerable<int>? deviceIds = null,
        int maxUses = 65535);

    /// <summary>Expire a raw daily-pass card number on the gate(s) immediately (card returned). Pass null for ALL devices.</summary>
    Task<(int ok, int fail, int total, List<string> errors)> ExpireTempCardOnDevicesAsync(
        string cardNumber, IEnumerable<int>? deviceIds = null);
    Task IncrementVisitAsync(string cardNumber);
    /// <summary>
    /// Real-time card validation on swipe: checks expiry + visits, increments visit count,
    /// and disables card on all devices in parallel if expired. Called from real-time monitor.
    /// </summary>
    Task<(bool isValid, string reason)> ValidateCardOnSwipeAsync(string cardNumber, bool isEntry = true);
    Task<int> GetCountAsync();
    Task<bool> IsCardNoDuplicateAsync(string cardNo, int? excludeId = null);
    Task<bool> IsPhoneDuplicateAsync(string phone, int? excludeId = null);
    Task<bool> IsNameDuplicateAsync(string fullNameEn, int? excludeId = null);

    // Soft delete with reason
    Task<bool> SoftDeleteEmployeeAsync(int id, string reason);

    // Freeze / Unfreeze — pass deviceIds=null for ALL devices
    Task<bool> FreezePlayerAsync(int id, string reason, IEnumerable<int>? deviceIds = null);
    Task<bool> UnfreezePlayerAsync(int id, IEnumerable<int>? deviceIds = null);

    // Renew subscription — pass deviceIds=null for ALL devices
    Task<bool> RenewSubscriptionAsync(int id, string subscriptionType, int months, int customDays,
        decimal fee, decimal amountPaid, string doorPermissions, int effectiveTimes, IEnumerable<int>? deviceIds = null);

    // Player profile
    Task<PlayerProfileDto> GetPlayerProfileAsync(int id);

    // Bulk operations
    Task<(int success, int failed)> BulkFreezeAsync(IEnumerable<int> ids, string reason);
    Task<(int success, int failed)> BulkUnfreezeAsync(IEnumerable<int> ids);
    Task<(int success, int failed)> BulkExtendAsync(IEnumerable<int> ids, int days);

    // Upload all cards to selected devices, skipping already-synced cards
    Task<(int uploaded, int skipped, int failed, int total)> UploadAllCardsToDevicesAsync(
        IEnumerable<int> deviceIds, IProgress<(int current, int total, string cardNumber)>? progress = null);

    // Reports
    Task<IEnumerable<EmployeeDto>> GetExpiringAsync(DateTime from, DateTime to);
    Task<IEnumerable<EmployeeDto>> GetRenewedAsync(DateTime from, DateTime to);
    Task<IEnumerable<EmployeeDto>> GetFrozenPlayersAsync();
    Task<IEnumerable<EmployeeDto>> GetExpiredPlayersAsync();
}
