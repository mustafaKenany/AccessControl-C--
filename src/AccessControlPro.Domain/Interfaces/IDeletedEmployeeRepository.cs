using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IDeletedEmployeeRepository
{
    Task AddAsync(DeletedEmployee entity);
    Task<IEnumerable<DeletedEmployee>> GetAllAsync();
    Task<(IEnumerable<DeletedEmployee> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null);
}
