using AccessControlPro.Application.DTOs;

namespace AccessControlPro.Application.Interfaces;

public interface IDeletedEmployeeService
{
    Task<(IEnumerable<DeletedEmployeeDto> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null);
}
