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
    /// <summary>Edit an existing PO (correct quantities/costs, apply a later discount). Adjusts stock by the
    /// net per-product delta and rebuilds the PO's stock movements. Blocks if it would push stock negative.</summary>
    Task UpdatePurchaseOrderAsync(PurchaseOrderDto dto);
    /// <summary>Delete a PO entirely: reverses its stock, removes its movements, reverses its expense entry, and updates supplier balances. Blocks if reversing stock would go negative.</summary>
    Task DeletePurchaseOrderAsync(int orderId);
    Task PayPurchaseOrderAsync(int orderId, decimal amount);

    // Stock Movements
    Task<IEnumerable<StockMovementDto>> GetAllStockMovementsAsync();
    Task<IEnumerable<StockMovementDto>> GetStockMovementsByProductAsync(int productId);
    /// <summary>Stock movements within a date range — for the movement/sales reports.</summary>
    Task<IEnumerable<StockMovementDto>> GetStockMovementsAsync(DateTime? from, DateTime? to);
}
