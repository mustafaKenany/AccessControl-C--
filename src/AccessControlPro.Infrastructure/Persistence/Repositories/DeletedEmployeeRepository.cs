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
            // SQL Server's default collation is case-insensitive — the old ToLower() forced a
            // computed scan and helped nothing. Use LIKE directly.
            var s = search.Trim();
            query = query.Where(e =>
                EF.Functions.Like(e.FullNameEn, "%" + s + "%") ||
                EF.Functions.Like(e.FullNameAr, "%" + s + "%") ||
                EF.Functions.Like(e.CardNo, "%" + s + "%") ||
                EF.Functions.Like(e.Phone, "%" + s + "%") ||
                EF.Functions.Like(e.DeleteReason, "%" + s + "%") ||
                EF.Functions.Like(e.DeletedBy, "%" + s + "%"));
        }

        var totalCount = await query.CountAsync();
        // Project WITHOUT PhotoData — the deleted-records list never shows the photo and there is no
        // restore flow, so reading the blob per row was pure waste.
        var items = await query
            .OrderByDescending(e => e.DeletedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new DeletedEmployee
            {
                Id = e.Id, OriginalId = e.OriginalId, FullNameEn = e.FullNameEn, FullNameAr = e.FullNameAr,
                CardNo = e.CardNo, SubscriptionType = e.SubscriptionType, Phone = e.Phone,
                SubscriptionFee = e.SubscriptionFee, AmountPaid = e.AmountPaid, StartDate = e.StartDate,
                EndDate = e.EndDate, Notes = e.Notes, DeleteReason = e.DeleteReason, DeletedBy = e.DeletedBy,
                DeletedAt = e.DeletedAt, OriginalCreatedAt = e.OriginalCreatedAt
            })
            .ToListAsync();

        return (items, totalCount);
    }
}
