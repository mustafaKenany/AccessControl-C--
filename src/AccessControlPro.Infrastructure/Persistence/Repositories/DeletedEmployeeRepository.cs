using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class DeletedEmployeeRepository : IDeletedEmployeeRepository
{
    private readonly AppDbContext _context;

    public DeletedEmployeeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DeletedEmployee entity)
    {
        _context.DeletedEmployees.Add(entity);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<DeletedEmployee>> GetAllAsync()
    {
        return await _context.DeletedEmployees
            .OrderByDescending(e => e.DeletedAt)
            .ToListAsync();
    }

    public async Task<(IEnumerable<DeletedEmployee> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null)
    {
        var query = _context.DeletedEmployees.AsQueryable();

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
