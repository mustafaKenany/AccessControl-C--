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
    private static void Log(string msg, string level = "info") =>
        RollingLogFile.Append(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}\n");

    private readonly IProductRepository _productRepo;
    private readonly ITransactionRepository _transactionRepo;
    private readonly IEmployeeRepository _employeeRepo;
    private readonly IStockMovementRepository _stockMovementRepo;
    private readonly IPosShiftRepository _shiftRepo;
    private readonly IPosTransactionRepository _posTxnRepo;
    private readonly CurrentUserService _currentUser;

    public PosService(
        IProductRepository productRepo,
        ITransactionRepository transactionRepo,
        IEmployeeRepository employeeRepo,
        IStockMovementRepository stockMovementRepo,
        IPosShiftRepository shiftRepo,
        IPosTransactionRepository posTxnRepo,
        CurrentUserService currentUser)
    {
        _productRepo = productRepo;
        _transactionRepo = transactionRepo;
        _employeeRepo = employeeRepo;
        _stockMovementRepo = stockMovementRepo;
        _shiftRepo = shiftRepo;
        _posTxnRepo = posTxnRepo;
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
        decimal discountAmount = 0, string discountReason = "", decimal? amountPaid = null)
    {
        if (items == null || items.Count == 0)
            return false;

        foreach (var item in items)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException($"Quantity must be positive for '{item.ProductName}'.");
            if (item.Price < 0)
                throw new ArgumentException($"Price cannot be negative for '{item.ProductName}'.");
            if (item.DiscountAmount < 0)
                throw new ArgumentException($"Discount cannot be negative for '{item.ProductName}'.");
        }
        if (discountAmount < 0)
            throw new ArgumentException("Order discount cannot be negative.");

        // Feature 1 — a sale can only happen inside an open cash-drawer shift, so every sale lands in
        // a shift's X/Z report and the drawer reconciles. Open a shift first (POS → Shift).
        if (await _shiftRepo.GetOpenShiftAsync() == null)
            throw new InvalidOperationException("No open shift. Open a shift before selling.");

        // Load the products FIRST and re-price every line from the DB — never trust the client-sent
        // price. The catalogue price in the database is authoritative; all money below is derived from
        // it. Stock + expiry are validated in the same pass before any deduction.
        var productIds = items.Select(i => i.ProductId).Distinct();
        var products = await _productRepo.GetByIdsAsync(productIds);
        var productMap = products.ToDictionary(p => p.Id);

        foreach (var item in items)
        {
            if (!productMap.TryGetValue(item.ProductId, out var product))
                throw new InvalidOperationException($"Product '{item.ProductName}' no longer exists.");
            if (item.Price != product.Price)
            {
                Log($"Re-priced '{item.ProductName}': client={item.Price} -> db={product.Price}", "warn");
                item.Price = product.Price;   // authoritative catalogue price
            }
            if (product.Stock < item.Quantity)
                throw new InvalidOperationException($"Insufficient stock for '{item.ProductName}'. Available: {product.Stock}, Requested: {item.Quantity}");
            // Feature 6 — don't sell perishables (supplements/vitamins) past their expiry date.
            if (product.ExpiryDate.HasValue && product.ExpiryDate.Value.Date < DateTime.Today)
                throw new InvalidOperationException($"'{item.ProductName}' expired on {product.ExpiryDate.Value:yyyy-MM-dd} and cannot be sold.");
        }

        var subtotal = items.Sum(i => i.Price * i.Quantity);
        var itemDiscounts = items.Sum(i => i.DiscountAmount);
        var totalDiscount = discountAmount + itemDiscounts;
        var totalAmount = subtotal - totalDiscount;
        if (totalAmount < 0) totalAmount = 0;

        // Work out how much is paid NOW vs taken on credit (debt).
        //   CardBalance : always paid in full from the wallet.
        //   Credit      : "on account" — defaults to the whole amount on credit (amountPaid overrides).
        //   Cash        : defaults to paid in full; a smaller amountPaid leaves the remainder as debt.
        decimal paidNow;
        if (method == PaymentMethod.CardBalance)
        {
            var employee = await _employeeRepo.GetByIdWithCardsAsync(employeeId ?? throw new ArgumentException("A player must be selected for card-balance payment."));
            if (employee == null || employee.CardBalance < totalAmount)
                return false;               // insufficient wallet — no charge, no stock change
            paidNow = totalAmount;
        }
        else if (method == PaymentMethod.Credit)
        {
            paidNow = amountPaid ?? 0m;
        }
        else
        {
            paidNow = amountPaid ?? totalAmount;
        }
        if (paidNow < 0) paidNow = 0;
        if (paidNow > totalAmount) paidNow = totalAmount;

        var debtAdded = method == PaymentMethod.CardBalance ? 0m : totalAmount - paidNow;
        if (debtAdded > 0 && !employeeId.HasValue)
            throw new ArgumentException("A player must be selected to sell on credit (leave a debt).");

        // Record the NET unit price on each movement (list price − this item's discount − its share of
        // the whole-order discount, allocated by line value) so the product-sales report shows revenue
        // actually earned, not the pre-discount list total.
        decimal NetUnitPrice(CartItemDto item)
        {
            var grossLine = item.Price * item.Quantity;
            var orderShare = subtotal > 0 ? discountAmount * (grossLine / subtotal) : 0m;
            var netLine = grossLine - item.DiscountAmount - orderShare;
            if (netLine < 0) netLine = 0;
            return item.Quantity > 0 ? Math.Round(netLine / item.Quantity, 4) : item.Price;
        }

        // One receipt number per sale — stamped on the income row AND every movement, so a later
        // refund can be matched back to exactly this sale (and its already-refunded quantities).
        var receiptNo = NewReceiptNo();

        var stockChanges = items
            .GroupBy(i => i.ProductId)
            .Select(g => (ProductId: g.Key, StockDelta: -g.Sum(i => i.Quantity)))
            .ToList();

        var movements = items.Select(item => new StockMovement
        {
            ProductId = item.ProductId,
            Type = MovementType.Out,
            Quantity = item.Quantity,
            UnitPrice = NetUnitPrice(item),
            Reference = "POS Sale",
            Description = "Sold to customer",
            ReceiptNo = receiptNo,
            RelatedEmployeeId = employeeId,   // the buyer (card/credit/member sales); null for walk-ins
            CreatedBy = _currentUser.Username ?? "System"
        }).ToList();

        var creditNote = debtAdded > 0 ? $" [credit: paid {paidNow:N0}, owes {debtAdded:N0}]" : "";
        var description = string.Join(", ", items.Select(i => $"{i.ProductName} x{i.Quantity}")) + creditNote;

        // Income = the cash actually taken now (keeps the drawer/shift right). A full-credit sale
        // (paid 0) records no cash row — only the goods leaving and the debt going up.
        Transaction? incomeTxn = paidNow <= 0 ? null : new Transaction
        {
            Type = TransactionType.Income,
            Category = "POS Sale",
            Amount = paidNow,
            Description = description,
            RelatedEmployeeId = employeeId,
            PaymentMethod = method,
            DiscountAmount = totalDiscount,
            DiscountReason = discountReason,
            ReceiptNo = receiptNo,
            CreatedBy = _currentUser.Username ?? "System"
        };

        // Build the whole write-set and persist it in ONE atomic transaction: stock deductions, stock
        // movements, the cash income, the card debit AND the player's debt all commit together or not
        // at all — so a mid-sale failure can't leave money/stock/debt half-applied, and concurrent
        // terminals can't oversell or overdraw (row-locked inside the engine).
        var cardDelta = method == PaymentMethod.CardBalance ? -totalAmount : 0m;
        var employeeToUpdate = (method == PaymentMethod.CardBalance || debtAdded > 0) ? employeeId : null;
        await _posTxnRepo.PersistAtomicAsync(stockChanges, movements, incomeTxn,
            employeeToUpdate, cardDelta, debtAdded);

        Log($"Sale OK: items={items.Count} total={totalAmount} paidNow={paidNow} debt={debtAdded} method={method} receipt={receiptNo} employeeId={(employeeId?.ToString() ?? "n/a")} by={_currentUser.Username}");
        return true;
    }

    // Short, sortable, unique-per-sale receipt id: a timestamp (invariant digits) + 12 hex of a Guid,
    // so even many sales in the same second don't collide. Fits the nvarchar(50) ReceiptNo column.
    private static string NewReceiptNo()
        => DateTime.Now.ToString("yyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture)
           + "-" + Guid.NewGuid().ToString("N")[..12];

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

    public async Task<decimal> GetDebtAsync(int employeeId)
    {
        var employee = await _employeeRepo.GetByIdWithCardsAsync(employeeId);
        return employee?.Debt ?? 0;
    }

    public async Task CollectDebtAsync(int employeeId, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Collection amount must be greater than zero.");

        var employee = await _employeeRepo.GetByIdWithCardsAsync(employeeId)
            ?? throw new InvalidOperationException($"Employee with ID {employeeId} not found.");

        if (amount > employee.Debt) amount = employee.Debt; // never collect more than owed
        if (amount <= 0) return;

        var debtBefore = employee.Debt;
        employee.Debt -= amount;
        if (employee.Debt < 0) employee.Debt = 0;
        await _employeeRepo.UpdateAsync(employee);

        // Record the cash received settling the credit sale.
        await _transactionRepo.AddAsync(new Transaction
        {
            Type = TransactionType.Income,
            Category = "Debt Collection",
            Amount = amount,
            Description = $"Debt payment from {employee.FullNameEn}",
            RelatedEmployeeId = employeeId,
            PaymentMethod = PaymentMethod.Cash,
            CreatedBy = _currentUser.Username ?? "System"
        });

        Log($"CollectDebt OK: employeeId={employeeId} player={employee.FullNameEn} amount={amount} before={debtBefore} after={employee.Debt}");
    }

    public async Task<IEnumerable<EmployeeDto>> GetPlayersWithDebtAsync()
    {
        var employees = await _employeeRepo.GetAllWithCardsAsync();
        return employees
            .Where(e => e.Debt > 0)
            .OrderByDescending(e => e.Debt)
            .Select(e => new EmployeeDto
            {
                Id = e.Id,
                FullNameEn = e.FullNameEn,
                FullNameAr = e.FullNameAr,
                CardNo = e.CardNo,
                Phone = e.Phone,
                Debt = e.Debt
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
            Log($"OpenShift: auto-closed {staleShifts.Count} stale shift(s)", "warn");
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

    // Builds the full cashier drawer report for a shift window. Shared by the live X report and the
    // Z (close) report so the expected-cash math can never diverge between them. Windows are in UTC to
    // match Transaction.CreatedAt. NOTE our income categories: "POS Sale" / "Card Top-Up" /
    // "Debt Collection"; refunds are Expense/"POS Refund" (populated once Feature 3 ships).
    private async Task<ShiftReportDto> BuildShiftReportAsync(PosShift shift, DateTime asOf, bool isClosed, decimal? closingCash = null)
    {
        async Task<List<Transaction>> Q(TransactionType type, string category)
            => (await _transactionRepo.GetPagedAsync(1, int.MaxValue, type,
                from: shift.OpenedAt, to: asOf, category: category)).Items.ToList();

        var sales = await Q(TransactionType.Income, "POS Sale");
        var refunds = await Q(TransactionType.Expense, "POS Refund");
        var topUps = await Q(TransactionType.Income, "Card Top-Up");
        var debtPays = await Q(TransactionType.Income, "Debt Collection");

        decimal Cash(IEnumerable<Transaction> ts) => ts.Where(t => t.PaymentMethod == PaymentMethod.Cash).Sum(t => t.Amount);

        var cashSales = Cash(sales);
        var cardSales = sales.Where(t => t.PaymentMethod == PaymentMethod.CardBalance).Sum(t => t.Amount);
        var cashTopUps = Cash(topUps);
        var cashDebtPayments = Cash(debtPays);
        var cashRefunds = Cash(refunds);
        var expected = shift.OpeningCash + cashSales + cashTopUps + cashDebtPayments - cashRefunds;

        return new ShiftReportDto
        {
            OpenedBy = shift.OpenedBy, OpenedAt = shift.OpenedAt, AsOf = asOf, IsClosed = isClosed,
            OpeningCash = shift.OpeningCash, CashSales = cashSales, CardSales = cardSales,
            CashTopUps = cashTopUps, CashDebtPayments = cashDebtPayments, CashRefunds = cashRefunds,
            ExpectedCash = expected, TotalSales = sales.Sum(t => t.Amount), SalesCount = sales.Count,
            TotalDiscounts = sales.Sum(t => t.DiscountAmount),
            ClosingCash = closingCash, Variance = closingCash.HasValue ? closingCash.Value - expected : null
        };
    }

    /// <summary>Live "X" report for the currently open shift (a snapshot that does NOT close it).</summary>
    public async Task<ShiftReportDto?> GetShiftReportAsync()
    {
        var shift = await GetOpenShiftAsync();
        return shift == null ? null : await BuildShiftReportAsync(shift, DateTime.UtcNow, isClosed: false);
    }

    public async Task<PosShift> CloseShiftAsync(decimal closingCash)
    {
        var shift = await GetOpenShiftAsync()
            ?? throw new InvalidOperationException("No open shift found.");

        var report = await BuildShiftReportAsync(shift, DateTime.UtcNow, isClosed: true, closingCash);
        shift.TotalSales = report.TotalSales;
        shift.TotalCashSales = report.CashSales;
        shift.TotalCardSales = report.CardSales;
        shift.ClosingCash = closingCash;
        shift.Variance = report.Variance!.Value;
        shift.ClosedAt = DateTime.UtcNow;
        shift.Status = "Closed";

        await _shiftRepo.UpdateAsync(shift);
        Log($"CloseShift OK: id={shift.Id} openingCash={shift.OpeningCash} cashSales={shift.TotalCashSales} cardSales={shift.TotalCardSales} closingCash={closingCash} variance={shift.Variance} expected={report.ExpectedCash}");
        return shift;
    }
}
