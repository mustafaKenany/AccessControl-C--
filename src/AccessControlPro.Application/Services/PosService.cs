using System.IO;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class PosService : IPosService
{
    // File log for POS — sale/refund/shift events go here. Captures the trail when
    // a customer says "I sold X but it doesn't show in receipts" or "shift balance off".
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "pos_log.txt");
    private static void Log(string msg) => RollingLogFile.Append(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");

    private readonly IProductRepository _productRepo;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IStockMovementRepository _stockMovementRepo;
    private readonly IPosShiftRepository _shiftRepo;
    private readonly CurrentUserService _currentUser;

    public PosService(
        IProductRepository productRepo,
        ITransactionRepository transactionRepo,
        IEmployeeRepository employeeRepo,
        IStockMovementRepository stockMovementRepo,
        IPosShiftRepository shiftRepo,
        CurrentUserService currentUser)
    {
        _productRepo = productRepo;
        _transactionRepo = transactionRepo;
        _employeeRepo = employeeRepo;
        _stockMovementRepo = stockMovementRepo;
        _shiftRepo = shiftRepo;
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

    public async Task<bool> SellAsync(List<CartItemDto> items, PaymentMethod method, int? employeeId = null,
        decimal discountAmount = 0, string discountReason = "")
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

        var subtotal = items.Sum(i => i.Price * i.Quantity);
        var itemDiscounts = items.Sum(i => i.DiscountAmount);
        var totalDiscount = discountAmount + itemDiscounts;
        var totalAmount = subtotal - totalDiscount;
        if (totalAmount < 0) totalAmount = 0;

        // Pre-validate ALL stock before any deduction
        var productIds = items.Select(i => i.ProductId).Distinct();
        var products = await _productRepo.GetByIdsAsync(productIds);
        var productMap = products.ToDictionary(p => p.Id);

        foreach (var item in items)
        {
            if (!productMap.TryGetValue(item.ProductId, out var product))
                throw new InvalidOperationException($"Product '{item.ProductName}' no longer exists.");
            if (product.Stock < item.Quantity)
                throw new InvalidOperationException($"Insufficient stock for '{item.ProductName}'. Available: {product.Stock}, Requested: {item.Quantity}");
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

        // Deduct stock + record stock movements
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
            DiscountAmount = totalDiscount,
            DiscountReason = discountReason,
            CreatedBy = _currentUser.Username ?? "System"
        });

        Log($"Sale OK: items={items.Count} subtotal={subtotal} discount={totalDiscount} total={totalAmount} method={method} employeeId={(employeeId?.ToString() ?? "n/a")} by={_currentUser.Username}");
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

        var balanceBefore = employee.CardBalance;
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

        Log($"TopUpCard OK: employeeId={employeeId} player={employee.FullNameEn} amount={amount} before={balanceBefore} after={employee.CardBalance}");
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

    public async Task<DailySummaryDto> GetDailySummaryAsync(DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        var (transactions, _) = await _transactionRepo.GetPagedAsync(
            1, int.MaxValue, TransactionType.Income,
            from: startOfDay, to: endOfDay, category: "POS Sale");

        var list = transactions.ToList();

        var cashSales = list.Where(t => t.PaymentMethod == PaymentMethod.Cash).Sum(t => t.Amount);
        var cardSales = list.Where(t => t.PaymentMethod == PaymentMethod.CardBalance).Sum(t => t.Amount);
        var totalDiscounts = list.Sum(t => t.DiscountAmount);

        // Count items from descriptions (format: "ProductName x2, ProductName x1")
        var totalItems = 0;
        foreach (var t in list)
        {
            if (string.IsNullOrEmpty(t.Description)) continue;
            var parts = t.Description.Split(',');
            foreach (var part in parts)
            {
                var xIdx = part.LastIndexOf(" x", StringComparison.Ordinal);
                if (xIdx >= 0 && int.TryParse(part[(xIdx + 2)..].Trim(), out var qty))
                    totalItems += qty;
                else
                    totalItems += 1;
            }
        }

        // Group by category (we parse product names from descriptions)
        // Since all POS sales are category "POS Sale", group by payment method instead
        var byCategory = new List<CategorySummaryDto>
        {
            new() { Category = "Cash", Count = list.Count(t => t.PaymentMethod == PaymentMethod.Cash), Total = cashSales },
            new() { Category = "Card Balance", Count = list.Count(t => t.PaymentMethod == PaymentMethod.CardBalance), Total = cardSales }
        };

        return new DailySummaryDto
        {
            Date = date.Date,
            TotalTransactions = list.Count,
            TotalSales = list.Sum(t => t.Amount),
            TotalCashSales = cashSales,
            TotalCardSales = cardSales,
            TotalDiscounts = totalDiscounts,
            TotalItemsSold = totalItems,
            ByCategory = byCategory.Where(c => c.Count > 0).ToList()
        };
    }

    // ── Shift Management ──

    public async Task<PosShift?> GetOpenShiftAsync()
    {
        return await _shiftRepo.GetOpenShiftAsync();
    }

    public async Task<PosShift> OpenShiftAsync(decimal openingCash)
    {
        // Close any stale open shifts first
        var staleShifts = await _shiftRepo.GetAllOpenShiftsAsync();
        foreach (var stale in staleShifts)
        {
            stale.Status = "Closed";
            stale.ClosedAt = DateTime.UtcNow;
        }
        if (staleShifts.Count > 0)
        {
            await _shiftRepo.UpdateRangeAsync(staleShifts);
            Log($"OpenShift: auto-closed {staleShifts.Count} stale shift(s)");
        }

        var shift = new PosShift
        {
            OpenedBy = _currentUser.Username ?? "System",
            OpenedAt = DateTime.UtcNow,
            OpeningCash = openingCash,
            Status = "Open"
        };

        await _shiftRepo.AddAsync(shift);
        Log($"OpenShift OK: id={shift.Id} openingCash={openingCash} openedBy={shift.OpenedBy}");
        return shift;
    }

    public async Task<PosShift> CloseShiftAsync(decimal closingCash)
    {
        var shift = await GetOpenShiftAsync()
            ?? throw new InvalidOperationException("No open shift found.");

        // Calculate sales during shift
        var (transactions, _) = await _transactionRepo.GetPagedAsync(
            1, int.MaxValue, TransactionType.Income,
            from: shift.OpenedAt, to: DateTime.UtcNow, category: "POS Sale");

        var txList = transactions.ToList();
        shift.TotalSales = txList.Sum(t => t.Amount);
        shift.TotalCashSales = txList.Where(t => t.PaymentMethod == PaymentMethod.Cash).Sum(t => t.Amount);
        shift.TotalCardSales = txList.Where(t => t.PaymentMethod == PaymentMethod.CardBalance).Sum(t => t.Amount);
        shift.ClosingCash = closingCash;
        shift.Variance = closingCash - (shift.OpeningCash + shift.TotalCashSales);
        shift.ClosedAt = DateTime.UtcNow;
        shift.Status = "Closed";

        await _shiftRepo.UpdateAsync(shift);
        Log($"CloseShift OK: id={shift.Id} openingCash={shift.OpeningCash} cashSales={shift.TotalCashSales} cardSales={shift.TotalCardSales} closingCash={closingCash} variance={shift.Variance}");
        return shift;
    }
}
