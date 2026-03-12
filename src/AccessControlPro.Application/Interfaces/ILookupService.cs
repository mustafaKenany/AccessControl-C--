using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Application.Interfaces;

public interface ILookupService
{
    Task<List<LookupItem>> GetByCategoryAsync(string category);
    Task AddAsync(LookupItem item);
    Task UpdateAsync(LookupItem item);
    Task DeleteAsync(int id);
}
