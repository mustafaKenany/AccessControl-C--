using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _repository;

    public AuditLogService(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<(IEnumerable<AuditLogDto> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null)
    {
        var (items, totalCount) = await _repository.GetPagedAsync(page, pageSize, search);
        var dtos = items.Select(log => new AuditLogDto
        {
            Id = log.Id,
            Action = log.Action,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            Details = log.Details,
            Timestamp = log.Timestamp
        });
        return (dtos, totalCount);
    }
}
