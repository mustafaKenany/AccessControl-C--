using AccessControlPro.Application.DTOs;

namespace AccessControlPro.Application.Interfaces;

public interface IQrPassService
{
    Task<QrPassDto> CreatePassAsync(string playerName, string phone, decimal fee, int maxUses = 5, int validDays = 1,
        int? deviceId = null, int doorNumber = 1, string deviceName = "", string incomeCategory = "Daily Pass");
    Task<(bool IsValid, string Message, QrPassDto? Pass)> ValidateAndUseAsync(string passCode);
    Task<(IEnumerable<QrPassDto> Items, int TotalCount)> GetPassesPagedAsync(
        int page, int pageSize, string? search = null, bool? activeOnly = null);
    Task<QrPassDto?> GetPassByIdAsync(int id);
    Task DeactivatePassAsync(int id);
    Task<int> ExpireOldPassesAsync();
    Task<int> GetActiveTodayCountAsync();
}
