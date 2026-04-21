using AccessControlPro.Application.DTOs;

namespace AccessControlPro.Application.Interfaces;

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
    Task<(int synced, int failed, int total)> SyncAllCardsToDeviceAsync(int deviceId, IProgress<(int current, int total, string cardNumber)>? progress = null);
    /// <summary>Sync all cards to selected devices. Pass null for ALL devices.</summary>
    Task<(int synced, int failed, int total)> SyncAllCardsToDevicesAsync(IEnumerable<int>? deviceIds = null, IProgress<(int current, int total, string cardNumber)>? progress = null);
    /// <summary>Remove/expire card on selected devices. Pass null for ALL devices.</summary>
    Task<(int synced, int failed, int total, List<string> errors)> RemoveCardFromDevicesAsync(int cardId, IEnumerable<int>? deviceIds = null);
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
