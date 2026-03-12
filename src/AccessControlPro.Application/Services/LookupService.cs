using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class LookupService : ILookupService
{
    private readonly ILookupRepository _repo;

    public LookupService(ILookupRepository repo)
    {
        _repo = repo;
    }

    public Task<List<LookupItem>> GetByCategoryAsync(string category)
        => _repo.GetByCategoryAsync(category);

    public Task AddAsync(LookupItem item)
        => _repo.AddAsync(item);

    public Task UpdateAsync(LookupItem item)
        => _repo.UpdateAsync(item);

    public Task DeleteAsync(int id)
        => _repo.DeleteAsync(id);
}
