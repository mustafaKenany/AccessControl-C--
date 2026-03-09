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
    Task<int> GetCountAsync();
    Task<bool> IsCardNoDuplicateAsync(string cardNo, int? excludeId = null);
    Task<bool> IsPhoneDuplicateAsync(string phone, int? excludeId = null);
    Task<bool> IsNameDuplicateAsync(string fullNameEn, int? excludeId = null);

    // Soft delete with reason
    Task<bool> SoftDeleteEmployeeAsync(int id, string reason);

    // Freeze / Unfreeze
    Task<bool> FreezePlayerAsync(int id, string reason);
    Task<bool> UnfreezePlayerAsync(int id);

    // Renew subscription
    Task<bool> RenewSubscriptionAsync(int id, string subscriptionType, int months, decimal fee, decimal amountPaid);
}
