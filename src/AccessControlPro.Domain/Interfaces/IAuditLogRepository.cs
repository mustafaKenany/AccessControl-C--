using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
    Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null);
}
