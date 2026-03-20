using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public EmployeeRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<Employee>> GetAllWithCardsAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees.Include(e => e.AccessCards).ToListAsync();
    }

    public async Task<(IEnumerable<Employee> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.Employees.Include(e => e.AccessCards).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(e =>
                (e.FullNameEn ?? "").ToLower().Contains(s) ||
                (e.FullNameAr ?? "").Contains(search) ||
                (e.CardNo ?? "").ToLower().Contains(s) ||
                (e.SubscriptionType ?? "").ToLower().Contains(s) ||
                (e.Phone ?? "").Contains(search));
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
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees.Include(e => e.AccessCards).FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Employee?> GetByEmployeeCodeAsync(string cardNo)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees.FirstOrDefaultAsync(e => e.CardNo == cardNo);
    }

    public async Task<Employee?> GetByPhoneAsync(string phone)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees.FirstOrDefaultAsync(e => e.Phone == phone);
    }

    public async Task<bool> ExistsByNameAsync(string fullNameEn, int? excludeId = null)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.Employees.Where(e => e.FullNameEn == fullNameEn);
        if (excludeId.HasValue)
            query = query.Where(e => e.Id != excludeId.Value);
        return await query.AnyAsync();
    }

    public async Task AddAsync(Employee employee)
    {
        await using var db = _factory.CreateDbContext();
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Employee employee)
    {
        await using var db = _factory.CreateDbContext();
        db.Employees.Update(employee);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        var employee = await db.Employees.FindAsync(id);
        if (employee != null)
        {
            db.Employees.Remove(employee);
            await db.SaveChangesAsync();
        }
    }

    public async Task<int> GetCountAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees.CountAsync();
    }

    public async Task<IEnumerable<Employee>> GetBySubscriptionEndDateRangeAsync(DateTime from, DateTime to)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees.Include(e => e.AccessCards)
            .Where(e => e.EndDate >= from && e.EndDate <= to)
            .OrderBy(e => e.EndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetByStartDateRangeAsync(DateTime from, DateTime to)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees.Include(e => e.AccessCards)
            .Where(e => e.StartDate >= from && e.StartDate <= to)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetFrozenAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees.Include(e => e.AccessCards)
            .Where(e => e.IsFrozen)
            .OrderBy(e => e.FreezeStartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetExpiredAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees.Include(e => e.AccessCards)
            .Where(e => e.EndDate < DateTime.UtcNow && !e.IsFrozen)
            .OrderByDescending(e => e.EndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetOutstandingBalancesAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees
            .Where(e => e.SubscriptionFee > e.AmountPaid)
            .OrderByDescending(e => e.SubscriptionFee - e.AmountPaid)
            .ToListAsync();
    }
}
