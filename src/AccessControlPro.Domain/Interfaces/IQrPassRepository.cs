using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IQrPassRepository
{
    Task<QrPass?> GetByIdAsync(int id);
    Task<QrPass?> GetByPassCodeAsync(string passCode);
    Task<(IEnumerable<QrPass> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search = null, bool? activeOnly = null);
    Task AddAsync(QrPass qrPass);
    Task UpdateAsync(QrPass qrPass);
    Task<int> GetActiveTodayCountAsync();
    Task<IEnumerable<QrPass>> GetExpiredActivePassesAsync();
}
