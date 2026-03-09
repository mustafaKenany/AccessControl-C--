using AccessControlPro.Application.DTOs;

namespace AccessControlPro.Application.Interfaces;

public interface IAuditLogService
{
    Task<(IEnumerable<AuditLogDto> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null);
}
