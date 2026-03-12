using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public UserRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<AppUser?> GetByIdAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Users.FindAsync(id);
    }

    public async Task<AppUser?> GetByUsernameAsync(string username)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Users
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<IEnumerable<AppUser>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Users.OrderBy(u => u.Username).ToListAsync();
    }

    public async Task AddAsync(AppUser user)
    {
        await using var db = _factory.CreateDbContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(AppUser user)
    {
        await using var db = _factory.CreateDbContext();
        db.Users.Update(user);
        await db.SaveChangesAsync();
    }
}
