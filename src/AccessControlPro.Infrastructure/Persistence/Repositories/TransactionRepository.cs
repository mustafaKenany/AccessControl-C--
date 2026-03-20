using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public TransactionRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<(IEnumerable<Transaction> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, TransactionType? type = null,
        DateTime? from = null, DateTime? to = null, string? search = null,
        string? category = null)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.Transactions.Include(t => t.RelatedEmployee).AsQueryable();

        if (type.HasValue)
            query = query.Where(t => t.Type == type.Value);
        if (from.HasValue)
            query = query.Where(t => t.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(t => t.CreatedAt <= to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Description.Contains(search) || t.Category.Contains(search));
        if (!string.IsNullOrWhiteSpace(category))
        {
            var arCategory = CategoryEnToAr(category);
            if (arCategory != null)
                query = query.Where(t => t.Category == category || t.Category == arCategory);
            else
                query = query.Where(t => t.Category == category);
        }

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Transaction?> GetByIdAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Transactions.Include(t => t.RelatedEmployee).FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task AddAsync(Transaction transaction)
    {
        await using var db = _factory.CreateDbContext();
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync();
    }

    public async Task<decimal> GetTotalByTypeAsync(TransactionType type, DateTime? from = null, DateTime? to = null)
    {
        await using var db = _factory.CreateDbContext();
        var query = db.Transactions.Where(t => t.Type == type);
        if (from.HasValue) query = query.Where(t => t.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(t => t.CreatedAt <= to.Value.Date.AddDays(1));
        return await query.SumAsync(t => (decimal?)t.Amount) ?? 0;
    }

    public async Task<IEnumerable<Transaction>> GetRecentAsync(int count)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Transactions.Include(t => t.RelatedEmployee)
            .OrderByDescending(t => t.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetByEmployeeIdAsync(int employeeId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Transactions
            .Where(t => t.RelatedEmployeeId == employeeId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task NullifyEmployeeIdAsync(int employeeId)
    {
        await using var db = _factory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE Transactions SET RelatedEmployeeId = NULL WHERE RelatedEmployeeId = {0}",
            employeeId);
    }

    private static readonly Dictionary<string, string> _enToAr = new()
    {
        ["Subscription"] = "اشتراك", ["POS Sales"] = "مبيعات",
        ["Owner Deposit"] = "إيداع المالك", ["Other"] = "أخرى",
        ["Rent"] = "إيجار", ["Electricity"] = "كهرباء", ["Water"] = "ماء",
        ["Salaries"] = "رواتب", ["Equipment"] = "معدات", ["Maintenance"] = "صيانة",
        ["Supplies"] = "مستلزمات", ["Marketing"] = "تسويق"
    };

    private static string? CategoryEnToAr(string en)
        => _enToAr.TryGetValue(en, out var ar) ? ar : null;
}
