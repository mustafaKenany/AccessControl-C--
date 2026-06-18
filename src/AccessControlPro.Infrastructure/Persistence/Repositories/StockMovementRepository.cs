using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class StockMovementRepository : IStockMovementRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public StockMovementRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<StockMovement>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.StockMovements
            .Include(m => m.Product)
            .Include(m => m.PurchaseOrder)
                .ThenInclude(po => po!.Supplier)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetByProductIdAsync(int productId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.StockMovements
            .Include(m => m.Product)
            .Include(m => m.PurchaseOrder)
                .ThenInclude(po => po!.Supplier)
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(StockMovement movement)
    {
        await using var db = _factory.CreateDbContext();
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetByPurchaseOrderAsync(int purchaseOrderId)
    {
        await using var db = _factory.CreateDbContext();
        return await db.StockMovements
            .Where(m => m.PurchaseOrderId == purchaseOrderId)
            .ToListAsync();
    }

    public async Task DeleteByPurchaseOrderAsync(int purchaseOrderId)
    {
        await using var db = _factory.CreateDbContext();
        var rows = await db.StockMovements
            .Where(m => m.PurchaseOrderId == purchaseOrderId)
            .ToListAsync();
        if (rows.Count == 0) return;
        db.StockMovements.RemoveRange(rows);
        await db.SaveChangesAsync();
    }
}
