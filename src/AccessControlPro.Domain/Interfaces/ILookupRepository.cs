using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface ILookupRepository
{
    Task<List<LookupItem>> GetByCategoryAsync(string category);
    Task<LookupItem?> GetByIdAsync(int id);
    Task AddAsync(LookupItem item);
    Task UpdateAsync(LookupItem item);
    Task DeleteAsync(int id);
}
