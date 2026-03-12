using AccessControlPro.Application.DTOs;

namespace AccessControlPro.Application.Interfaces;

public interface IInventoryService
{
    // Products (admin CRUD)
    Task<IEnumerable<ProductDto>> GetAllProductsAsync();
    Task AddProductAsync(ProductDto dto);
    Task UpdateProductAsync(ProductDto dto);
    Task DeleteProductAsync(int id);

    // Purchase Orders
    Task<IEnumerable<PurchaseOrderDto>> GetAllPurchaseOrdersAsync();
    Task CreatePurchaseOrderAsync(PurchaseOrderDto dto);
    Task PayPurchaseOrderAsync(int orderId, decimal amount);

    // Stock Movements
    Task<IEnumerable<StockMovementDto>> GetAllStockMovementsAsync();
    Task<IEnumerable<StockMovementDto>> GetStockMovementsByProductAsync(int productId);
}
