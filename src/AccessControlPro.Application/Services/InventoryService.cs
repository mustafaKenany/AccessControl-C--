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
            CostPrice = p.CostPrice,
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
            CostPrice = dto.CostPrice,
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
        product.CostPrice = dto.CostPrice;
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

        // Batch-load all products in one query, then update stock + record movements
        var productIds = dto.Items.Select(i => i.ProductId).Distinct();
        var productList = (await _productRepo.GetByIdsAsync(productIds)).ToDictionary(p => p.Id);

        foreach (var item in dto.Items)
        {
            if (productList.TryGetValue(item.ProductId, out var product))
            {
                // Weighted-average cost: new cost = (existing stock value + this purchase value)
                // / total units. So buying the same item at different prices over time settles
                // on a true average cost — which is what the profit/reports rely on. Falls back
                // to the new unit cost when there's no prior stock or no prior cost recorded.
                var oldStock = product.Stock;
                var oldCost = product.CostPrice;
                if (item.UnitCost > 0)
                {
                    product.CostPrice = (oldStock > 0 && oldCost > 0)
                        ? Math.Round(((oldStock * oldCost) + (item.Quantity * item.UnitCost)) / (oldStock + item.Quantity), 2)
                        : item.UnitCost;
                }
                product.Stock = oldStock + item.Quantity;
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

        // Record as expense transaction, tagged with the PO reference so an edit can find and adjust it.
        var description = string.Join(", ", dto.Items.Select(i => $"{i.ProductName} x{i.Quantity}"));
        await _transactionRepo.AddAsync(new Transaction
        {
            Type = TransactionType.Expense,
            Category = "Purchase Order",
            Amount = order.TotalAmount,
            Description = description,
            Reference = $"PO-{order.Id}",
            PaymentMethod = PaymentMethod.Cash,
            CreatedBy = _currentUser.Username ?? "System"
        });
    }

    public async Task UpdatePurchaseOrderAsync(PurchaseOrderDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Id <= 0)
            throw new ArgumentException("Invalid purchase order.");
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

        var original = await _poRepo.GetByIdWithItemsAsync(dto.Id)
            ?? throw new InvalidOperationException($"Purchase order with ID {dto.Id} not found.");

        // Net stock delta per product = sum(new quantities) - sum(original quantities).
        // A removed line reverses its full original quantity; an added line adds its full new quantity.
        var delta = new Dictionary<int, int>();
        foreach (var it in original.Items)
            delta[it.ProductId] = delta.GetValueOrDefault(it.ProductId) - it.Quantity;
        foreach (var it in dto.Items)
            delta[it.ProductId] = delta.GetValueOrDefault(it.ProductId) + it.Quantity;

        var affected = (await _productRepo.GetByIdsAsync(delta.Keys.ToList())).ToDictionary(p => p.Id);

        // Guard: never let an edit drive stock negative (e.g. units already sold since purchase).
        foreach (var kv in delta)
        {
            if (kv.Value != 0 && affected.TryGetValue(kv.Key, out var p) && p.Stock + kv.Value < 0)
                throw new InvalidOperationException(
                    $"Cannot update: stock for '{p.Name}' would become negative ({p.Stock} on hand). " +
                    "Some units were likely already sold.");
        }

        // Apply the stock delta. (Weighted-average cost is intentionally NOT recomputed on edit —
        // editing corrects history rather than re-running the averaging.)
        foreach (var kv in delta)
        {
            if (kv.Value != 0 && affected.TryGetValue(kv.Key, out var p))
            {
                p.Stock += kv.Value;
                await _productRepo.UpdateAsync(p);
            }
        }

        var totalAmount = dto.Items.Sum(i => i.TotalCost);
        var net = totalAmount - dto.Discount;
        await _poRepo.UpdateWithItemsAsync(new PurchaseOrder
        {
            Id = dto.Id,
            SupplierId = dto.SupplierId,
            OrderDate = original.OrderDate,
            TotalAmount = totalAmount,
            Discount = dto.Discount,
            AmountPaid = dto.AmountPaid,
            PaymentStatus = dto.AmountPaid >= net ? "Paid" : dto.AmountPaid > 0 ? "Partial" : "Unpaid",
            Notes = dto.Notes,
            Items = dto.Items.Select(i => new PurchaseOrderItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitCost = i.UnitCost
            }).ToList()
        });

        // Rebuild this PO's stock movements so the movement log matches the corrected quantities.
        await _stockMovementRepo.DeleteByPurchaseOrderAsync(dto.Id);
        foreach (var item in dto.Items)
        {
            await _stockMovementRepo.AddAsync(new StockMovement
            {
                ProductId = item.ProductId,
                Type = MovementType.In,
                Quantity = item.Quantity,
                UnitPrice = item.UnitCost,
                Reference = $"PO-{dto.Id}",
                Description = "Purchase from supplier (edited)",
                PurchaseOrderId = dto.Id,
                CreatedBy = _currentUser.Username ?? "System"
            });
        }

        // Keep Cash Flow in sync: update this PO's expense entry to the corrected total.
        var reference = $"PO-{dto.Id}";
        var description2 = string.Join(", ", dto.Items.Select(i => $"{i.ProductName} x{i.Quantity}"));
        var expense = await _transactionRepo.GetByReferenceAsync(reference);
        if (expense != null)
        {
            expense.Amount = totalAmount;
            expense.Description = description2;
            await _transactionRepo.UpdateAsync(expense);
        }
        else
        {
            // Legacy PO created before expense entries were tagged: record a difference entry so
            // finance totals still reconcile (positive = extra expense, negative = refund/income).
            var diff = totalAmount - original.TotalAmount;
            if (diff != 0)
            {
                await _transactionRepo.AddAsync(new Transaction
                {
                    Type = diff > 0 ? TransactionType.Expense : TransactionType.Income,
                    Category = "Purchase Order",
                    Amount = Math.Abs(diff),
                    Description = $"PO-{dto.Id} edit adjustment: {description2}",
                    Reference = reference,
                    PaymentMethod = PaymentMethod.Cash,
                    CreatedBy = _currentUser.Username ?? "System"
                });
            }
        }
    }

    public async Task DeletePurchaseOrderAsync(int orderId)
    {
        var order = await _poRepo.GetByIdWithItemsAsync(orderId)
            ?? throw new InvalidOperationException($"Purchase order with ID {orderId} not found.");

        // Reverse the stock this PO added. Guard against going negative (units already sold).
        var byProduct = order.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var affected = (await _productRepo.GetByIdsAsync(byProduct.Keys.ToList())).ToDictionary(p => p.Id);
        foreach (var kv in byProduct)
        {
            if (affected.TryGetValue(kv.Key, out var p) && p.Stock - kv.Value < 0)
                throw new InvalidOperationException(
                    $"Cannot delete: stock for '{p.Name}' would become negative ({p.Stock} on hand). " +
                    "Some units were likely already sold.");
        }
        foreach (var kv in byProduct)
        {
            if (affected.TryGetValue(kv.Key, out var p))
            {
                p.Stock -= kv.Value;
                await _productRepo.UpdateAsync(p);
            }
        }

        // Remove the PO's stock movements.
        await _stockMovementRepo.DeleteByPurchaseOrderAsync(orderId);

        // Reverse the expense in Cash Flow: delete the tagged entry, or (legacy) record an income reversal.
        var expense = await _transactionRepo.GetByReferenceAsync($"PO-{orderId}");
        if (expense != null)
        {
            await _transactionRepo.DeleteAsync(expense.Id);
        }
        else if (order.TotalAmount > 0)
        {
            await _transactionRepo.AddAsync(new Transaction
            {
                Type = TransactionType.Income,
                Category = "Purchase Order",
                Amount = order.TotalAmount,
                Description = $"PO-{orderId} deleted (reversal)",
                Reference = $"PO-{orderId}",
                PaymentMethod = PaymentMethod.Cash,
                CreatedBy = _currentUser.Username ?? "System"
            });
        }

        // Finally remove the order (line items cascade).
        await _poRepo.DeleteAsync(orderId);
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

    public async Task<IEnumerable<StockMovementDto>> GetStockMovementsAsync(DateTime? from, DateTime? to)
    {
        var movements = await _stockMovementRepo.GetAllAsync();
        var q = movements.AsEnumerable();
        if (from.HasValue) q = q.Where(m => m.CreatedAt >= from.Value.Date);
        if (to.HasValue) q = q.Where(m => m.CreatedAt < to.Value.Date.AddDays(1));
        return q.OrderByDescending(m => m.CreatedAt).Select(MapToDto);
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
