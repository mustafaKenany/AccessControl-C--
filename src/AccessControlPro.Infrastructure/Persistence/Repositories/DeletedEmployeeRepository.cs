using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class DeletedEmployeeRepository : IDeletedEmployeeRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DeletedEmployeeRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task AddAsync(DeletedEmployee entity)
    {
        await using var db = _factory.CreateDbContext();
        db.DeletedEmployees.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<DeletedEmployee>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.DeletedEmployees
            .OrderByDescending(e => e.DeletedAt)
            .ToListAsync();
    }

    public async Task<(IEnumerable<DeletedEmployee> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.DeletedEmployees.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(e =>
                e.FullNameEn.ToLower().Contains(s) ||
                e.FullNameAr.Contains(s) ||
                e.CardNo.ToLower().Contains(s) ||
                e.Phone.Contains(s) ||
                e.DeleteReason.ToLower().Contains(s) ||
                e.DeletedBy.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.DeletedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
