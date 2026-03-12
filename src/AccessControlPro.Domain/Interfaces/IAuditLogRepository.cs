using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
    Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null, DateTime? from = null, DateTime? to = null);
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, int entityId);
}
