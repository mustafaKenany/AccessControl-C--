using AccessControlPro.Application.DTOs;
using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Application.Interfaces;

public interface IPosService
{
    // Products
    Task<IEnumerable<ProductDto>> GetAllProductsAsync();
    Task AddProductAsync(ProductDto dto);
    Task UpdateProductAsync(ProductDto dto);
    Task DeleteProductAsync(int id);

    // Barcode lookup
    Task<ProductDto?> GetByBarcodeAsync(string barcode);

    // Sales
    Task<bool> SellAsync(List<CartItemDto> items, PaymentMethod method, int? employeeId = null);

    // Card balance
    Task<decimal> GetCardBalanceAsync(int employeeId);
    Task TopUpCardAsync(int employeeId, decimal amount);

    // Today's sales summary
    Task<(int Count, decimal Total)> GetTodaySalesAsync();
}

public class CartItemDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal Total => Price * Quantity;
}
