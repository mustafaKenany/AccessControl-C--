using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly AppDbContext _context;

    public EmployeeRepository(AppDbContext context) => _context = context;

    public async Task<IEnumerable<Employee>> GetAllWithCardsAsync()
        => await _context.Employees.Include(e => e.AccessCards).ToListAsync();

    public async Task<(IEnumerable<Employee> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null)
    {
        var query = _context.Employees.Include(e => e.AccessCards).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(e =>
                e.FullNameEn.ToLower().Contains(s) ||
                e.FullNameAr.Contains(search) ||
                e.CardNo.ToLower().Contains(s) ||
                e.SubscriptionType.ToLower().Contains(s) ||
                e.Phone.Contains(search));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Employee?> GetByIdWithCardsAsync(int id)
        => await _context.Employees.Include(e => e.AccessCards).FirstOrDefaultAsync(e => e.Id == id);

    public async Task<Employee?> GetByEmployeeCodeAsync(string cardNo)
        => await _context.Employees.FirstOrDefaultAsync(e => e.CardNo == cardNo);

    public async Task<Employee?> GetByPhoneAsync(string phone)
        => await _context.Employees.FirstOrDefaultAsync(e => e.Phone == phone);

    public async Task<bool> ExistsByNameAsync(string fullNameEn, int? excludeId = null)
    {
        var query = _context.Employees.Where(e => e.FullNameEn == fullNameEn);
        if (excludeId.HasValue)
            query = query.Where(e => e.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task AddAsync(Employee employee)
    {
        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Employee employee)
    {
        _context.Employees.Update(employee);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee != null)
        {
            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetCountAsync()
        => await _context.Employees.CountAsync();
}
