using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Domain.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(int id);
    Task<AppUser?> GetByUsernameAsync(string username);
    Task<IEnumerable<AppUser>> GetAllAsync();
    Task AddAsync(AppUser user);
    Task UpdateAsync(AppUser user);
}
