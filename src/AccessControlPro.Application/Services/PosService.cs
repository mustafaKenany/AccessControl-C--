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
            CostPrice = p.CostPrice,
            Category = p.Category,
            Stock = p.Stock,
            ReorderLevel = p.ReorderLevel,
            ExpiryDate = p.ExpiryDate,
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
            CostPrice = p.CostPrice,
            Category = p.Category,
            Stock = p.Stock,
            ReorderLevel = p.ReorderLevel,
            ExpiryDate = p.ExpiryDate,
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

    // ── Feature 3 — Refund tied to the original sale ──

    /// <summary>The most recent sales (one per receipt) with per-line remaining-refundable quantities,
    /// for the "refund from a recent sale" picker.</summary>
    public async Task<List<PosSaleDto>> GetRecentSalesAsync(int count = 30)
    {
        var movements = (await _stockMovementRepo.GetRecentSaleMovementsAsync(count)).ToList();
        var sales = new List<PosSaleDto>();
        foreach (var g in movements.Where(m => !string.IsNullOrEmpty(m.ReceiptNo)).GroupBy(m => m.ReceiptNo))
        {
            var outs = g.Where(m => m.Type == MovementType.Out).ToList();
            if (outs.Count == 0) continue;
            var returns = g.Where(m => m.Type == MovementType.Return).ToList();

            var lines = outs.GroupBy(m => m.ProductId).Select(pg => new PosSaleLineDto
            {
                ProductId = pg.Key,
                ProductName = pg.First().Product?.Name ?? $"#{pg.Key}",
                UnitPrice = pg.First().UnitPrice,
                SoldQty = pg.Sum(x => x.Quantity),
                RefundedQty = returns.Where(r => r.ProductId == pg.Key).Sum(r => r.Quantity)
            }).ToList();

            sales.Add(new PosSaleDto
            {
                ReceiptNo = g.Key,
                Time = outs.Max(m => m.CreatedAt),
                Total = lines.Sum(l => l.UnitPrice * l.SoldQty),
                RefundableTotal = lines.Sum(l => l.UnitPrice * l.RefundableQty),
                ItemsSummary = string.Join(", ", lines.Select(l => $"{l.ProductName} x{l.SoldQty}")),
                Lines = lines
            });
        }
        return sales.OrderByDescending(s => s.Time).ToList();
    }

    /// <summary>Refund specific items from a past receipt: never more than remains un-refunded, at the
    /// exact net price paid, back to the original payment instrument. Cancels the sale's outstanding
    /// debt first, then refunds only the paid fraction as money. Anti-fraud + atomic.</summary>
    public async Task<bool> RefundSaleAsync(string receiptNo, List<CartItemDto> items, string reason = "")
    {
        if (string.IsNullOrWhiteSpace(receiptNo)) throw new ArgumentException("A sale receipt is required.");
        if (items == null || items.Count == 0) return false;

        var movements = (await _stockMovementRepo.GetByReceiptNoAsync(receiptNo)).ToList();
        var outs = movements.Where(m => m.Type == MovementType.Out).ToList();
        if (outs.Count == 0) throw new InvalidOperationException("Original sale not found for this receipt.");
        var returns = movements.Where(m => m.Type == MovementType.Return).ToList();

        var refundMovements = new List<StockMovement>();
        var stockChanges = new List<(int ProductId, int StockDelta)>();
        decimal refundAmount = 0;
        var buyerId = outs[0].RelatedEmployeeId;   // the sale's buyer (from the sale movements)

        foreach (var item in items)
        {
            if (item.Quantity <= 0) continue;
            var soldLines = outs.Where(o => o.ProductId == item.ProductId).ToList();
            if (soldLines.Count == 0)
                throw new InvalidOperationException($"'{item.ProductName}' was not part of this sale.");
            var sold = soldLines.Sum(o => o.Quantity);
            var alreadyRefunded = returns.Where(r => r.ProductId == item.ProductId).Sum(r => r.Quantity);
            var remaining = sold - alreadyRefunded;
            if (item.Quantity > remaining)
                throw new InvalidOperationException($"Cannot refund {item.Quantity} of '{item.ProductName}': only {remaining} remain refundable on this receipt.");

            var unit = soldLines[0].UnitPrice;   // refund at the exact net price paid — never current price
            refundAmount += unit * item.Quantity;
            stockChanges.Add((item.ProductId, item.Quantity));   // stock returns to shelf
            refundMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                Type = MovementType.Return,
                Quantity = item.Quantity,
                UnitPrice = unit,
                Reference = "POS Refund",
                Description = string.IsNullOrWhiteSpace(reason) ? "Customer return" : reason,
                ReceiptNo = receiptNo,
                RelatedEmployeeId = buyerId,
                CreatedBy = _currentUser.Username ?? "System"
            });
        }
        if (refundMovements.Count == 0) return false;

        // Derive the ORIGINAL sale's payment method + how much was actually PAID vs taken on debt.
        // Returning goods cancels THIS sale's outstanding debt first, then refunds the rest as MONEY to
        // the original instrument. Never cancels more than the player's current debt, and never pays out
        // for a portion that was taken on credit and never paid.
        var saleTxn = await _transactionRepo.GetPosSaleByReceiptAsync(receiptNo);
        var method = saleTxn?.PaymentMethod ?? PaymentMethod.Cash;
        if (saleTxn?.RelatedEmployeeId != null) buyerId = saleTxn.RelatedEmployeeId;
        var saleTotal = outs.Sum(o => o.UnitPrice * o.Quantity);
        var paidTotal = saleTxn?.Amount ?? 0m;

        var paidFraction = saleTotal > 0 ? Math.Min(1m, paidTotal / saleTotal) : 1m;
        var moneyRefund = Math.Round(refundAmount * paidFraction, 2);
        var creditPortion = refundAmount - moneyRefund;
        decimal debtReduce = 0m;
        if (buyerId.HasValue && creditPortion > 0)
        {
            var currentDebt = await GetDebtAsync(buyerId.Value);
            debtReduce = Math.Min(creditPortion, currentDebt);
        }

        // Cash/card money-out is booked as an expense only for the portion actually refunded as money.
        Transaction? expenseTxn = moneyRefund <= 0 ? null : new Transaction
        {
            Type = TransactionType.Expense,
            Category = "POS Refund",
            Amount = moneyRefund,
            Description = $"Refund for receipt {receiptNo}" + (string.IsNullOrWhiteSpace(reason) ? "" : $" — {reason}"),
            RelatedEmployeeId = buyerId,
            PaymentMethod = method,
            ReceiptNo = receiptNo,
            CreatedBy = _currentUser.Username ?? "System"
        };

        var cardDelta = method == PaymentMethod.CardBalance ? moneyRefund : 0m;   // credit the original wallet
        var empToUpdate = (method == PaymentMethod.CardBalance || debtReduce > 0) ? buyerId : null;
        await _posTxnRepo.PersistAtomicAsync(stockChanges, refundMovements, expenseTxn, empToUpdate, cardDelta, -debtReduce);
        Log($"RefundSale OK: receipt={receiptNo} refund={refundAmount} money={moneyRefund} debtReduced={debtReduce} method={method} by={_currentUser.Username}");
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

        var pay = Math.Min(amount, employee.Debt); // never collect more than owed
        if (pay <= 0) return;

        // Record the cash income AND lower the debt atomically. The engine also clears DebtSince when
        // the debt reaches 0, so the aging clock resets correctly and the drawer stays consistent.
        var incomeTxn = new Transaction
        {
            Type = TransactionType.Income,
            Category = "Debt Collection",
            Amount = pay,
            Description = $"Debt payment from {employee.FullNameEn}",
            RelatedEmployeeId = employeeId,
            PaymentMethod = PaymentMethod.Cash,
            CreatedBy = _currentUser.Username ?? "System"
        };
        await _posTxnRepo.PersistAtomicAsync(
            Array.Empty<(int, int)>(), Array.Empty<StockMovement>(), incomeTxn, employeeId, 0m, -pay);

        Log($"CollectDebt OK: employeeId={employeeId} player={employee.FullNameEn} amount={pay} before={employee.Debt} after={employee.Debt - pay}");
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
                Debt = e.Debt,
                DebtSince = e.DebtSince
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
