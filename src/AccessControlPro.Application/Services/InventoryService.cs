using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IProductRepository _productRepo;
    private readonly IPurchaseOrderRepository _poRepo;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IStockMovementRepository _stockMovementRepo;
    private readonly CurrentUserService _currentUser;

    public InventoryService(
        IProductRepository productRepo,
        IPurchaseOrderRepository poRepo,
        ITransactionRepository transactionRepo,
        IStockMovementRepository stockMovementRepo,
        CurrentUserService currentUser)
    {
        _productRepo = productRepo;
        _poRepo = poRepo;
        _transactionRepo = transactionRepo;
        _stockMovementRepo = stockMovementRepo;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<ProductDto>> GetAllProductsAsync()
    {
        var products = await _productRepo.GetAllAsync();
        return products.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            NameAr = p.NameAr,
            Barcode = p.Barcode,
            Price = p.Price,
            Category = p.Category,
            Stock = p.Stock,
            IsActive = p.IsActive
        });
    }

    public async Task AddProductAsync(ProductDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Product name is required.");
        if (dto.Price < 0)
            throw new ArgumentException("Price cannot be negative.");
        if (dto.Stock < 0)
            throw new ArgumentException("Stock cannot be negative.");

        await _productRepo.AddAsync(new Product
        {
            Name = dto.Name,
            NameAr = dto.NameAr,
            Barcode = dto.Barcode,
            Price = dto.Price,
            Category = dto.Category,
            Stock = dto.Stock,
            IsActive = true
        });
    }

    public async Task UpdateProductAsync(ProductDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Product name is required.");
        if (dto.Price < 0)
            throw new ArgumentException("Price cannot be negative.");
        if (dto.Stock < 0)
            throw new ArgumentException("Stock cannot be negative.");

        var product = await _productRepo.GetByIdAsync(dto.Id)
            ?? throw new InvalidOperationException($"Product with ID {dto.Id} not found.");

        product.Name = dto.Name;
        product.NameAr = dto.NameAr;
        product.Barcode = dto.Barcode;
        product.Price = dto.Price;
        product.Category = dto.Category;
        product.Stock = dto.Stock;
        product.IsActive = dto.IsActive;
        await _productRepo.UpdateAsync(product);
    }

    public async Task DeleteProductAsync(int id)
    {
        await _productRepo.DeleteAsync(id);
    }

    public async Task<IEnumerable<PurchaseOrderDto>> GetAllPurchaseOrdersAsync()
    {
        var orders = await _poRepo.GetAllAsync();
        return orders.Select(po => new PurchaseOrderDto
        {
            Id = po.Id,
            SupplierId = po.SupplierId,
            SupplierName = po.Supplier?.Name ?? "",
            OrderDate = po.OrderDate,
            TotalAmount = po.TotalAmount,
            Discount = po.Discount,
            AmountPaid = po.AmountPaid,
            PaymentStatus = po.PaymentStatus,
            Notes = po.Notes,
            CreatedBy = po.CreatedBy,
            Items = po.Items.Select(i => new PurchaseOrderItemDto
            {
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? "",
                Quantity = i.Quantity,
                UnitCost = i.UnitCost
            }).ToList()
        });
    }

    public async Task CreatePurchaseOrderAsync(PurchaseOrderDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Items == null || dto.Items.Count == 0)
            throw new ArgumentException("Purchase order must have at least one item.");
        if (dto.SupplierId <= 0)
            throw new ArgumentException("Supplier is required.");
        if (dto.Discount < 0)
            throw new ArgumentException("Discount cannot be negative.");
        if (dto.AmountPaid < 0)
            throw new ArgumentException("Amount paid cannot be negative.");
        foreach (var item in dto.Items)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException($"Quantity must be positive for product {item.ProductName}.");
            if (item.UnitCost < 0)
                throw new ArgumentException($"Unit cost cannot be negative for product {item.ProductName}.");
        }

        var order = new PurchaseOrder
        {
            SupplierId = dto.SupplierId,
            OrderDate = dto.OrderDate,
            TotalAmount = dto.Items.Sum(i => i.TotalCost),
            Discount = dto.Discount,
            AmountPaid = dto.AmountPaid,
            PaymentStatus = dto.AmountPaid >= dto.Items.Sum(i => i.TotalCost) - dto.Discount ? "Paid"
                : dto.AmountPaid > 0 ? "Partial" : "Unpaid",
            Notes = dto.Notes,
            CreatedBy = _currentUser.Username ?? "System",
            Items = dto.Items.Select(i => new PurchaseOrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitCost = i.UnitCost
            }).ToList()
        };

        await _poRepo.AddAsync(order);

        // Add stock to products + record stock movements
        foreach (var item in dto.Items)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId);
            if (product != null)
            {
                product.Stock += item.Quantity;
                await _productRepo.UpdateAsync(product);

                await _stockMovementRepo.AddAsync(new StockMovement
                {
                    ProductId = item.ProductId,
                    Type = MovementType.In,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitCost,
                    Reference = $"PO-{order.Id}",
                    Description = $"Purchase from supplier",
                    PurchaseOrderId = order.Id,
                    CreatedBy = _currentUser.Username ?? "System"
                });
            }
        }

        // Record as expense transaction
        var description = string.Join(", ", dto.Items.Select(i => $"{i.ProductName} x{i.Quantity}"));
        await _transactionRepo.AddAsync(new Transaction
        {
            Type = TransactionType.Expense,
            Category = "Purchase Order",
            Amount = order.TotalAmount,
            Description = description,
            PaymentMethod = PaymentMethod.Cash,
            CreatedBy = _currentUser.Username ?? "System"
        });
    }

    public async Task PayPurchaseOrderAsync(int orderId, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.");

        var order = await _poRepo.GetByIdWithItemsAsync(orderId)
            ?? throw new InvalidOperationException($"Purchase order with ID {orderId} not found.");

        var remaining = (order.TotalAmount - order.Discount) - order.AmountPaid;
        if (amount > remaining && remaining > 0)
            throw new ArgumentException($"Payment amount ({amount:C}) exceeds remaining balance ({remaining:C}).");

        order.AmountPaid += amount;
        var netAmount = order.TotalAmount - order.Discount;
        order.PaymentStatus = order.AmountPaid >= netAmount ? "Paid"
            : order.AmountPaid > 0 ? "Partial" : "Unpaid";
        await _poRepo.UpdateAsync(order);
    }

    public async Task<IEnumerable<StockMovementDto>> GetAllStockMovementsAsync()
    {
        var movements = await _stockMovementRepo.GetAllAsync();
        return movements.Select(MapToDto);
    }

    public async Task<IEnumerable<StockMovementDto>> GetStockMovementsByProductAsync(int productId)
    {
        var movements = await _stockMovementRepo.GetByProductIdAsync(productId);
        return movements.Select(MapToDto);
    }

    private static StockMovementDto MapToDto(StockMovement m) => new()
    {
        Id = m.Id,
        ProductId = m.ProductId,
        ProductName = m.Product?.Name ?? "",
        Type = m.Type,
        Quantity = m.Quantity,
        UnitPrice = m.UnitPrice,
        Reference = m.Reference,
        Description = m.Description,
        PurchaseOrderId = m.PurchaseOrderId,
        SupplierName = m.PurchaseOrder?.Supplier?.Name ?? "",
        CreatedBy = m.CreatedBy,
        CreatedAt = m.CreatedAt
    };
}
