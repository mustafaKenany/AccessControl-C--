using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class PosService : IPosService
{
    private readonly IProductRepository _productRepo;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IStockMovementRepository _stockMovementRepo;
    private readonly CurrentUserService _currentUser;

    public PosService(
        IProductRepository productRepo,
        ITransactionRepository transactionRepo,
        IEmployeeRepository employeeRepo,
        IStockMovementRepository stockMovementRepo,
        CurrentUserService currentUser)
    {
        _productRepo = productRepo;
        _transactionRepo = transactionRepo;
        _employeeRepo = employeeRepo;
        _stockMovementRepo = stockMovementRepo;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<ProductDto>> GetAllProductsAsync()
    {
        var products = await _productRepo.GetAllActiveAsync();
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

    public async Task<ProductDto?> GetByBarcodeAsync(string barcode)
    {
        var p = await _productRepo.GetByBarcodeAsync(barcode);
        if (p == null) return null;
        return new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            NameAr = p.NameAr,
            Barcode = p.Barcode,
            Price = p.Price,
            Category = p.Category,
            Stock = p.Stock,
            IsActive = p.IsActive
        };
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
        product.Price = dto.Price;
        product.Category = dto.Category;
        product.Stock = dto.Stock;
        await _productRepo.UpdateAsync(product);
    }

    public async Task DeleteProductAsync(int id)
    {
        await _productRepo.DeleteAsync(id);
    }

    public async Task<bool> SellAsync(List<CartItemDto> items, PaymentMethod method, int? employeeId = null)
    {
        if (items == null || items.Count == 0)
            return false;

        foreach (var item in items)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException($"Quantity must be positive for '{item.ProductName}'.");
            if (item.Price < 0)
                throw new ArgumentException($"Price cannot be negative for '{item.ProductName}'.");
        }

        if (method == PaymentMethod.CardBalance && !employeeId.HasValue)
            throw new ArgumentException("Employee ID is required for card balance payment.");

        var totalAmount = items.Sum(i => i.Total);

        // Pre-validate ALL stock before any deduction
        var productMap = new Dictionary<int, Product>();
        foreach (var item in items)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId);
            if (product == null)
                throw new InvalidOperationException($"Product '{item.ProductName}' no longer exists.");
            if (product.Stock < item.Quantity)
                throw new InvalidOperationException($"Insufficient stock for '{item.ProductName}'. Available: {product.Stock}, Requested: {item.Quantity}");
            productMap[item.ProductId] = product;
        }

        // If paying by card balance, check sufficient funds
        if (method == PaymentMethod.CardBalance && employeeId.HasValue)
        {
            var employee = await _employeeRepo.GetByIdWithCardsAsync(employeeId.Value);
            if (employee == null || employee.CardBalance < totalAmount)
                return false;

            employee.CardBalance -= totalAmount;
            await _employeeRepo.UpdateAsync(employee);
        }

        // Deduct stock + record stock movements (all pre-validated)
        foreach (var item in items)
        {
            var product = productMap[item.ProductId];
            product.Stock -= item.Quantity;
            await _productRepo.UpdateAsync(product);

            await _stockMovementRepo.AddAsync(new StockMovement
            {
                ProductId = item.ProductId,
                Type = MovementType.Out,
                Quantity = item.Quantity,
                UnitPrice = item.Price,
                Reference = "POS Sale",
                Description = $"Sold to customer",
                CreatedBy = _currentUser.Username ?? "System"
            });
        }

        // Create income transaction
        var description = string.Join(", ", items.Select(i => $"{i.ProductName} x{i.Quantity}"));
        await _transactionRepo.AddAsync(new Transaction
        {
            Type = TransactionType.Income,
            Category = "POS Sale",
            Amount = totalAmount,
            Description = description,
            RelatedEmployeeId = employeeId,
            PaymentMethod = method,
            CreatedBy = _currentUser.Username ?? "System"
        });

        return true;
    }

    public async Task<decimal> GetCardBalanceAsync(int employeeId)
    {
        var employee = await _employeeRepo.GetByIdWithCardsAsync(employeeId);
        return employee?.CardBalance ?? 0;
    }

    public async Task TopUpCardAsync(int employeeId, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Top-up amount must be greater than zero.");

        var employee = await _employeeRepo.GetByIdWithCardsAsync(employeeId)
            ?? throw new InvalidOperationException($"Employee with ID {employeeId} not found.");

        employee.CardBalance += amount;
        await _employeeRepo.UpdateAsync(employee);

        // Record as income transaction
        await _transactionRepo.AddAsync(new Transaction
        {
            Type = TransactionType.Income,
            Category = "Card Top-Up",
            Amount = amount,
            Description = $"Card top-up for {employee.FullNameEn}",
            RelatedEmployeeId = employeeId,
            PaymentMethod = PaymentMethod.Cash,
            CreatedBy = _currentUser.Username ?? "System"
        });
    }

    public async Task<(int Count, decimal Total)> GetTodaySalesAsync()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var (transactions, _) = await _transactionRepo.GetPagedAsync(
            1, int.MaxValue, TransactionType.Income,
            from: today, to: tomorrow, category: "POS Sale");

        var list = transactions.ToList();
        return (list.Count, list.Sum(t => t.Amount));
    }
}
