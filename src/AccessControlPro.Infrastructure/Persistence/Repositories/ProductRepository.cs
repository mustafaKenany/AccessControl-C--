using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public ProductRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<IEnumerable<Product>> GetAllActiveAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Products.Where(p => p.IsActive).OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        await using var db = _factory.CreateDbContext();
        return await db.Products.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Products.FindAsync(id);
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Products.FirstOrDefaultAsync(p => p.Barcode == barcode && p.IsActive);
    }

    public async Task AddAsync(Product product)
    {
        await using var db = _factory.CreateDbContext();
        db.Products.Add(product);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Product product)
    {
        await using var db = _factory.CreateDbContext();
        db.Products.Update(product);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        var product = await db.Products.FindAsync(id);
        if (product != null)
        {
            product.IsActive = false;
            await db.SaveChangesAsync();
        }
    }
}
