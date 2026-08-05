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

    // Photo-free, AsNoTracking projection for the 10-second expiry monitor. The old path loaded every
    // player's full JPEG (PhotoData byte[]) + card graph, tracked, every 10s → ~108 MB/min of dead
    // photo churn that fragmented the 32-bit heap into OutOfMemoryException. Here EF selects only the
    // scalar fields it needs and computes "has an active synced card" as a SQL EXISTS — no blobs.
    public async Task<IReadOnlyList<ExpiryMonitorRow>> GetExpiryMonitorRowsAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees.AsNoTracking()
            .Select(e => new ExpiryMonitorRow(
                e.Id, e.FullNameEn, e.IsFrozen, e.EndDate, e.MaxVisits, e.UsedVisits,
                e.AccessCards.Any(c => c.IsActive && c.IsSyncedToDevice)))
            .ToListAsync();
    }

    // Digit-aware search: a card number (or a scanned card) uses an INDEXED exact/prefix seek — no
    // full-table scan — which is what made card-scan search slow. Text searches use contains on the
    // name/phone. Shared by the paged list, the filter pills, and the has-card count.
    private static IQueryable<Employee> ApplySearch(IQueryable<Employee> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var s = search.Trim();
        if (s.All(char.IsDigit))
        {
            var z = s.TrimStart('0');
            return query.Where(e =>
                e.CardNo == s || e.CardNo == z
                || EF.Functions.Like(e.CardNo, s + "%")
                || (z.Length > 0 && EF.Functions.Like(e.CardNo, z + "%"))
                || EF.Functions.Like(e.Phone, s + "%"));
        }
        return query.Where(e =>
            EF.Functions.Like(e.FullNameEn, "%" + s + "%")
            || EF.Functions.Like(e.FullNameAr, "%" + s + "%")
            || EF.Functions.Like(e.Phone, "%" + s + "%"));
    }

    // List/search/filter queries must NOT read the PhotoData blob (the grid shows no photo — reading
    // 100 photo blobs per page was the dominant slowdown). Project every scalar EXCEPT PhotoData;
    // cards have no blob so load them fully. The photo is loaded on demand by GetByIdWithCardsAsync.
    private static IQueryable<Employee> ProjectListShape(IQueryable<Employee> query) => query.Select(e => new Employee
    {
        Id = e.Id, FullNameEn = e.FullNameEn, FullNameAr = e.FullNameAr, CardNo = e.CardNo,
        SubscriptionType = e.SubscriptionType, Phone = e.Phone, Height = e.Height, Weight = e.Weight,
        SubscriptionFee = e.SubscriptionFee, Discount = e.Discount, AmountPaid = e.AmountPaid,
        StartDate = e.StartDate, EndDate = e.EndDate, Notes = e.Notes, IsFrozen = e.IsFrozen,
        FreezeStartDate = e.FreezeStartDate, CardBalance = e.CardBalance, Debt = e.Debt,
        MaxVisits = e.MaxVisits, UsedVisits = e.UsedVisits, CreatedAt = e.CreatedAt,
        AccessCards = e.AccessCards.ToList()
    });

    public async Task<(IEnumerable<Employee> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null)
    {
        await using var db = _factory.CreateDbContext();
        var query = ApplySearch(db.Employees.AsQueryable(), search);
        var totalCount = await query.CountAsync();
        var items = await ProjectListShape(
            query.OrderByDescending(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync();
        return (items, totalCount);
    }

    // DB-level paging for the filter pills (Expiring/Renewed/Frozen/Expired/Active) so clicking a
    // filter no longer loads the WHOLE matching set at once. Returns the page + total + has-card count.
    public async Task<(IEnumerable<Employee> Items, int TotalCount, int WithCardCount)> GetFilteredPagedAsync(
        int filter, DateTime from, DateTime to, int page, int pageSize, string? search = null)
    {
        await using var db = _factory.CreateDbContext();
        var now = DateTime.UtcNow;
        var query = db.Employees.AsQueryable();

        query = filter switch
        {
            1 => query.Where(e => e.EndDate >= from && e.EndDate <= to),   // Expiring soon
            2 => query.Where(e => e.StartDate >= from && e.StartDate <= to), // Renewed
            3 => query.Where(e => e.IsFrozen),                              // Frozen
            4 => query.Where(e => e.EndDate < now && !e.IsFrozen),         // Expired
            5 => query.Where(e => e.EndDate >= now && !e.IsFrozen),        // Active
            _ => query
        };

        query = ApplySearch(query, search);

        var total = await query.CountAsync();
        var withCard = await query.CountAsync(e => e.AccessCards.Any());

        IOrderedQueryable<Employee> ordered = filter switch
        {
            1 => query.OrderBy(e => e.EndDate),
            2 => query.OrderByDescending(e => e.StartDate),
            3 => query.OrderBy(e => e.FreezeStartDate),
            4 => query.OrderByDescending(e => e.EndDate),
            5 => query.OrderBy(e => e.EndDate),
            _ => query.OrderByDescending(e => e.Id)
        };

        var items = await ProjectListShape(ordered.Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync();
        return (items, total, withCard);
    }

    public async Task<int> GetWithCardCountAsync(string? search = null)
    {
        await using var db = _factory.CreateDbContext();
        var query = ApplySearch(db.Employees.AsQueryable(), search);
        return await query.CountAsync(e => e.AccessCards.Any());
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

    public async Task<IEnumerable<Employee>> GetActiveAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees.Include(e => e.AccessCards)
            .Where(e => e.EndDate >= DateTime.UtcNow && !e.IsFrozen)
            .OrderBy(e => e.EndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Employee>> GetOutstandingBalancesAsync(string? search = null, int take = 500)
    {
        await using var db = _factory.CreateDbContext();
        // Search in the DB (was: load ALL outstanding then filter in C#) + cap the display list, and
        // project WITHOUT the PhotoData blob (never shown). The full unpaid TOTAL comes from
        // GetTotalOutstandingAsync (a DB SUM), so capping the list here doesn't skew the total.
        var query = db.Employees.Where(e => e.SubscriptionFee > e.AmountPaid);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(e =>
                EF.Functions.Like(e.FullNameEn, "%" + s + "%") ||
                EF.Functions.Like(e.FullNameAr, "%" + s + "%") ||
                EF.Functions.Like(e.CardNo, "%" + s + "%"));
        }
        return await query
            .OrderByDescending(e => e.SubscriptionFee - e.AmountPaid)
            .Take(take)
            .Select(e => new Employee
            {
                Id = e.Id, FullNameEn = e.FullNameEn, FullNameAr = e.FullNameAr, CardNo = e.CardNo,
                Phone = e.Phone, SubscriptionType = e.SubscriptionType, SubscriptionFee = e.SubscriptionFee,
                Discount = e.Discount, AmountPaid = e.AmountPaid, CardBalance = e.CardBalance, Debt = e.Debt,
                StartDate = e.StartDate, EndDate = e.EndDate
            })
            .ToListAsync();
    }

    public async Task<decimal> GetTotalOutstandingAsync()
    {
        await using var db = _factory.CreateDbContext();
        // DB-side SUM of net owed across ALL outstanding members — no rows loaded into memory.
        return await db.Employees
            .Where(e => e.SubscriptionFee > e.AmountPaid)
            .SumAsync(e => e.SubscriptionFee - e.AmountPaid);
    }

    public async Task<IEnumerable<(int Id, string CardNo, DateTime StartDate, DateTime EndDate, int MaxVisits)>> GetCardInfoForSyncAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Employees
            .Where(e => !string.IsNullOrEmpty(e.CardNo))
            .Select(e => new { e.Id, e.CardNo, e.StartDate, e.EndDate, e.MaxVisits })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(e => (e.Id, e.CardNo, e.StartDate, e.EndDate, e.MaxVisits)));
    }
}
