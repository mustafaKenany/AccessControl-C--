using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PurchaseOrderRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<PurchaseOrder>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Items).ThenInclude(i => i.Product)
            .OrderByDescending(po => po.OrderDate)
            .ToListAsync();
    }

    public async Task<PurchaseOrder?> GetByIdWithItemsAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(po => po.Id == id);
    }

    public async Task AddAsync(PurchaseOrder order)
    {
        await using var db = _factory.CreateDbContext();
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(PurchaseOrder order)
    {
        await using var db = _factory.CreateDbContext();
        db.PurchaseOrders.Update(order);
        await db.SaveChangesAsync();
    }
}
