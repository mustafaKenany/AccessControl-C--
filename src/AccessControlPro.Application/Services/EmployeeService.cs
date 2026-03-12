using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;

namespace AccessControlPro.Application.Services;

public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IAccessCardRepository _cardRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IAccessControlSdk _sdk;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IDeletedEmployeeRepository _deletedEmployeeRepository;
    private readonly IFreezeHistoryRepository _freezeHistoryRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICardDeviceSyncRepository _cardDeviceSyncRepository;
    private readonly CurrentUserService _currentUser;
    private readonly ISessionLogger _sessionLogger;

    public EmployeeService(
        IEmployeeRepository employeeRepository,
        IAccessCardRepository cardRepository,
        IDeviceRepository deviceRepository,
        IAccessControlSdk sdk,
        IAuditLogRepository auditLogRepository,
        IDeletedEmployeeRepository deletedEmployeeRepository,
        IFreezeHistoryRepository freezeHistoryRepository,
        ITransactionRepository transactionRepository,
        ICardDeviceSyncRepository cardDeviceSyncRepository,
        CurrentUserService currentUser,
        ISessionLogger sessionLogger)
    {
        _employeeRepository = employeeRepository;
        _cardRepository = cardRepository;
        _deviceRepository = deviceRepository;
        _sdk = sdk;
        _auditLogRepository = auditLogRepository;
        _deletedEmployeeRepository = deletedEmployeeRepository;
        _freezeHistoryRepository = freezeHistoryRepository;
        _transactionRepository = transactionRepository;
        _cardDeviceSyncRepository = cardDeviceSyncRepository;
        _currentUser = currentUser;
        _sessionLogger = sessionLogger;
    }

    public async Task<IEnumerable<EmployeeDto>> GetAllEmployeesAsync()
    {
        var employees = await _employeeRepository.GetAllWithCardsAsync();
        return employees.Select(e => ToDto(e));
    }

    public async Task<(IEnumerable<EmployeeDto> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search = null)
    {
        var (items, totalCount) = await _employeeRepository.GetPagedAsync(page, pageSize, search);
        return (items.Select(e => ToDto(e)), totalCount);
    }

    public async Task<EmployeeDto?> GetEmployeeByIdAsync(int id)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        return employee == null ? null : ToDto(employee);
    }

    public async Task AddEmployeeAsync(EmployeeDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.FullNameEn))
            throw new ArgumentException("English name is required.");
        if (string.IsNullOrWhiteSpace(dto.CardNo))
            throw new ArgumentException("Card number is required.");
        if (dto.SubscriptionFee < 0)
            throw new ArgumentException("Subscription fee cannot be negative.");
        if (dto.AmountPaid < 0)
            throw new ArgumentException("Amount paid cannot be negative.");

        var existing = await _employeeRepository.GetByEmployeeCodeAsync(dto.CardNo);
        if (existing != null)
            throw new InvalidOperationException($"DUPLICATE_CARD:{dto.CardNo}");

        var existingPhone = await _employeeRepository.GetByPhoneAsync(dto.Phone);
        if (existingPhone != null)
            throw new InvalidOperationException($"DUPLICATE_PHONE:{dto.Phone}");

        var employee = new Employee
        {
            FullNameEn = dto.FullNameEn,
            FullNameAr = dto.FullNameAr,
            CardNo = dto.CardNo,
            SubscriptionType = dto.SubscriptionType,
            Phone = dto.Phone,
            PhotoData = dto.PhotoData,
            Height = dto.Height,
            Weight = dto.Weight,
            SubscriptionFee = dto.SubscriptionFee,
            AmountPaid = dto.AmountPaid,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Notes = dto.Notes
        };
        await _employeeRepository.AddAsync(employee);

        // Auto-create income transaction for initial subscription payment
        if (dto.AmountPaid > 0)
        {
            await _transactionRepository.AddAsync(new Transaction
            {
                Type = TransactionType.Income,
                Category = "Subscription",
                Amount = dto.AmountPaid,
                Description = $"New subscription: {dto.FullNameEn} ({dto.SubscriptionType})",
                RelatedEmployeeId = employee.Id,
                PaymentMethod = PaymentMethod.Cash,
                CreatedBy = _currentUser.Username ?? "System"
            });
        }

        await LogAuditAsync("Create", "Player", employee.Id,
            $"Added player: {dto.FullNameEn} ({dto.CardNo})",
            $"تم إضافة لاعب: {dto.FullNameAr} ({dto.CardNo})");

        // Log to session file
        await _sessionLogger.LogOperationAsync("CREATE", "Player", employee.Id,
            $"Added player: {dto.FullNameEn} ({dto.CardNo}) - Fee: {dto.SubscriptionFee}",
            $"تم إضافة لاعب: {dto.FullNameAr} ({dto.CardNo}) - الرسوم: {dto.SubscriptionFee}",
            _currentUser.Username);
    }

    public async Task<bool> UpdateEmployeeAsync(EmployeeDto dto, string? editReason = null)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(dto.Id);
        if (employee == null) return false;

        if (employee.CardNo != dto.CardNo)
        {
            var existingCard = await _employeeRepository.GetByEmployeeCodeAsync(dto.CardNo);
            if (existingCard != null && existingCard.Id != dto.Id)
                throw new InvalidOperationException($"DUPLICATE_CARD:{dto.CardNo}");
        }

        if (employee.Phone != dto.Phone)
        {
            var existingPhone = await _employeeRepository.GetByPhoneAsync(dto.Phone);
            if (existingPhone != null && existingPhone.Id != dto.Id)
                throw new InvalidOperationException($"DUPLICATE_PHONE:{dto.Phone}");
        }

        // Detect which fields actually changed
        var changesEn = new List<string>();
        var changesAr = new List<string>();

        if (employee.FullNameEn != dto.FullNameEn)
        {
            changesEn.Add($"Name(EN): {employee.FullNameEn} → {dto.FullNameEn}");
            changesAr.Add($"الاسم(EN): {employee.FullNameEn} → {dto.FullNameEn}");
        }
        if (employee.FullNameAr != dto.FullNameAr)
        {
            changesEn.Add($"Name(AR): {employee.FullNameAr} → {dto.FullNameAr}");
            changesAr.Add($"الاسم(AR): {employee.FullNameAr} → {dto.FullNameAr}");
        }
        if (employee.CardNo != dto.CardNo)
        {
            changesEn.Add($"CardNo: {employee.CardNo} → {dto.CardNo}");
            changesAr.Add($"رقم البطاقة: {employee.CardNo} → {dto.CardNo}");
        }
        if (employee.Phone != dto.Phone)
        {
            changesEn.Add($"Phone: {employee.Phone} → {dto.Phone}");
            changesAr.Add($"الهاتف: {employee.Phone} → {dto.Phone}");
        }
        if (employee.SubscriptionType != dto.SubscriptionType)
        {
            changesEn.Add($"Subscription: {employee.SubscriptionType} → {dto.SubscriptionType}");
            changesAr.Add($"الاشتراك: {employee.SubscriptionType} → {dto.SubscriptionType}");
        }
        if (employee.SubscriptionFee != dto.SubscriptionFee)
        {
            changesEn.Add($"Fee: {employee.SubscriptionFee} → {dto.SubscriptionFee}");
            changesAr.Add($"الرسوم: {employee.SubscriptionFee} → {dto.SubscriptionFee}");
        }
        if (employee.AmountPaid != dto.AmountPaid)
        {
            changesEn.Add($"Paid: {employee.AmountPaid} → {dto.AmountPaid}");
            changesAr.Add($"المدفوع: {employee.AmountPaid} → {dto.AmountPaid}");
        }
        if (employee.StartDate.Date != dto.StartDate.Date)
        {
            changesEn.Add($"StartDate: {employee.StartDate:yyyy-MM-dd} → {dto.StartDate:yyyy-MM-dd}");
            changesAr.Add($"تاريخ البدء: {employee.StartDate:yyyy-MM-dd} → {dto.StartDate:yyyy-MM-dd}");
        }
        if (employee.EndDate.Date != dto.EndDate.Date)
        {
            changesEn.Add($"EndDate: {employee.EndDate:yyyy-MM-dd} → {dto.EndDate:yyyy-MM-dd}");
            changesAr.Add($"تاريخ الانتهاء: {employee.EndDate:yyyy-MM-dd} → {dto.EndDate:yyyy-MM-dd}");
        }
        if (employee.Height != dto.Height)
        {
            changesEn.Add($"Height: {employee.Height} → {dto.Height}");
            changesAr.Add($"الطول: {employee.Height} → {dto.Height}");
        }
        if (employee.Weight != dto.Weight)
        {
            changesEn.Add($"Weight: {employee.Weight} → {dto.Weight}");
            changesAr.Add($"الوزن: {employee.Weight} → {dto.Weight}");
        }
        if (employee.Notes != dto.Notes)
        {
            changesEn.Add("Notes changed");
            changesAr.Add("تم تغيير الملاحظات");
        }
        if (!PhotoEqual(employee.PhotoData, dto.PhotoData))
        {
            changesEn.Add("Photo changed");
            changesAr.Add("تم تغيير الصورة");
        }

        employee.FullNameEn = dto.FullNameEn;
        employee.FullNameAr = dto.FullNameAr;
        employee.CardNo = dto.CardNo;
        employee.SubscriptionType = dto.SubscriptionType;
        employee.Phone = dto.Phone;
        employee.PhotoData = dto.PhotoData;
        employee.Height = dto.Height;
        employee.Weight = dto.Weight;
        employee.SubscriptionFee = dto.SubscriptionFee;
        employee.AmountPaid = dto.AmountPaid;
        employee.StartDate = dto.StartDate;
        employee.EndDate = dto.EndDate;
        employee.Notes = dto.Notes;
        await _employeeRepository.UpdateAsync(employee);

        // Build audit message: auto-detected changes + user reason
        var changedFieldsEn = changesEn.Count > 0
            ? string.Join(", ", changesEn)
            : "No fields changed";
        var changedFieldsAr = changesAr.Count > 0
            ? string.Join(", ", changesAr)
            : "لا توجد تغييرات";

        var reasonEn = string.IsNullOrWhiteSpace(editReason) ? "" : $" | Reason: {editReason}";
        var reasonAr = string.IsNullOrWhiteSpace(editReason) ? "" : $" | السبب: {editReason}";

        await LogAuditAsync("Update", "Player", employee.Id,
            $"Updated player: {dto.FullNameEn} ({dto.CardNo}). Changes: [{changedFieldsEn}]{reasonEn}",
            $"تم تعديل لاعب: {dto.FullNameAr} ({dto.CardNo}). التغييرات: [{changedFieldsAr}]{reasonAr}");

        // Log to session file
        await _sessionLogger.LogOperationAsync("UPDATE", "Player", employee.Id,
            $"Updated player: {dto.FullNameEn} ({dto.CardNo}). Changes: [{changedFieldsEn}]",
            $"تم تعديل لاعب: {dto.FullNameAr} ({dto.CardNo}). التغييرات: [{changedFieldsAr}]",
            _currentUser.Username);

        return true;
    }

    private static bool PhotoEqual(byte[]? a, byte[]? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;
        return a.AsSpan().SequenceEqual(b);
    }

    public async Task<bool> DeleteEmployeeAsync(int id)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        if (employee == null) return false;

        await LogAuditAsync("Delete", "Player", id,
            $"Deleted player: {employee.FullNameEn} ({employee.CardNo})",
            $"تم حذف لاعب: {employee.FullNameAr} ({employee.CardNo})");

        // Log to session file
        await _sessionLogger.LogOperationAsync("DELETE", "Player", id,
            $"Deleted player: {employee.FullNameEn} ({employee.CardNo})",
            $"تم حذف لاعب: {employee.FullNameAr} ({employee.CardNo})",
            _currentUser.Username);

        await _employeeRepository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> SoftDeleteEmployeeAsync(int id, string reason)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        if (employee == null) return false;

        try
        {
            // Archive to DeletedEmployees table (financial data preserved)
            var archived = new DeletedEmployee
            {
                OriginalId = employee.Id,
                FullNameEn = employee.FullNameEn,
                FullNameAr = employee.FullNameAr,
                CardNo = employee.CardNo,
                SubscriptionType = employee.SubscriptionType,
                Phone = employee.Phone,
                PhotoData = employee.PhotoData,
                Height = employee.Height,
                Weight = employee.Weight,
                SubscriptionFee = employee.SubscriptionFee,
                AmountPaid = employee.AmountPaid,
                StartDate = employee.StartDate,
                EndDate = employee.EndDate,
                Notes = employee.Notes,
                DeleteReason = reason,
                DeletedBy = _currentUser.Username ?? "Admin",
                DeletedAt = DateTime.UtcNow,
                OriginalCreatedAt = employee.CreatedAt
            };
            await _deletedEmployeeRepository.AddAsync(archived);

            await LogAuditAsync("SoftDelete", "Player", id,
                $"Soft-deleted player: {employee.FullNameEn} ({employee.CardNo}). Reason: {reason}",
                $"تم حذف لاعب: {employee.FullNameAr} ({employee.CardNo}). السبب: {reason}");

            // Log to session file
            await _sessionLogger.LogOperationAsync("SOFT_DELETE", "Player", id,
                $"Soft-deleted player: {employee.FullNameEn} ({employee.CardNo}). Reason: {reason}",
                $"تم حذف لاعب: {employee.FullNameAr} ({employee.CardNo}). السبب: {reason}",
                _currentUser.Username);

            // Delete from active table - cascade will handle related records via FK constraints
            // If cascade delete is not configured, transactions will be preserved with NULL EmployeeId
            await _employeeRepository.DeleteAsync(id);
            return true;
        }
        catch (Exception ex)
        {
            // Extract the actual SQL exception message to detect constraint violations
            var errorMsg = ex.Message;
            var innerException = ex.InnerException?.Message ?? "";

            // Check for foreign key constraint violations (the actual error from database)
            if (errorMsg.Contains("constraint") || errorMsg.Contains("REFERENCE") ||
                innerException.Contains("constraint") || innerException.Contains("REFERENCE") ||
                innerException.Contains("FK_Transactions"))
            {
                // Log the error for debugging
                await _sessionLogger.LogErrorAsync("SOFT_DELETE", "Player", id,
                    $"Failed - player has associated records. Error: {innerException}",
                    _currentUser.Username);

                throw new InvalidOperationException(
                    $"Cannot delete player '{employee.FullNameEn}': This player has associated transaction records. " +
                    "The system preserves financial history. Error: foreign key constraint violation.",
                    ex);
            }

            // Log the error for debugging
            await _sessionLogger.LogErrorAsync("SOFT_DELETE", "Player", id,
                $"Failed to soft-delete player. Error: {ex.Message}",
                _currentUser.Username);

            // Re-throw with consistent message that includes "constraint" keyword for UI detection
            throw new InvalidOperationException(
                $"Failed to delete player: {employee.FullNameEn}. Error: {ex.Message} [constraint violation]",
                ex);
        }
    }

    public async Task<bool> FreezePlayerAsync(int id, string reason)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        if (employee == null || employee.IsFrozen) return false;

        employee.IsFrozen = true;
        employee.FreezeStartDate = DateTime.UtcNow;
        await _employeeRepository.UpdateAsync(employee);

        var freeze = new FreezeHistory
        {
            EmployeeId = id,
            FreezeStart = DateTime.UtcNow,
            Reason = reason
        };
        await _freezeHistoryRepository.AddAsync(freeze);

        // Disable all synced cards on hardware devices (overwrite with expired date)
        await DisableCardsOnHardware(employee);

        await LogAuditAsync("Freeze", "Player", id,
            $"Froze player: {employee.FullNameEn} ({employee.CardNo}). Reason: {reason}",
            $"تم تجميد لاعب: {employee.FullNameAr} ({employee.CardNo}). السبب: {reason}");

        // Log to session file
        await _sessionLogger.LogOperationAsync("FREEZE", "Player", id,
            $"Froze player: {employee.FullNameEn} ({employee.CardNo}). Reason: {reason}",
            $"تم تجميد لاعب: {employee.FullNameAr} ({employee.CardNo}). السبب: {reason}",
            _currentUser.Username);

        return true;
    }

    public async Task<bool> UnfreezePlayerAsync(int id)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        if (employee == null || !employee.IsFrozen || employee.FreezeStartDate == null) return false;

        var freezeDays = (int)Math.Ceiling((DateTime.UtcNow - employee.FreezeStartDate.Value).TotalDays);
        if (freezeDays < 1) freezeDays = 1;

        // Extend subscription EndDate by freeze duration
        employee.EndDate = employee.EndDate.AddDays(freezeDays);
        employee.IsFrozen = false;
        employee.FreezeStartDate = null;
        await _employeeRepository.UpdateAsync(employee);

        // Close the active freeze record
        var activeFreeze = await _freezeHistoryRepository.GetActiveFreezeAsync(id);
        if (activeFreeze != null)
        {
            activeFreeze.FreezeEnd = DateTime.UtcNow;
            activeFreeze.FreezeDays = freezeDays;
            await _freezeHistoryRepository.UpdateAsync(activeFreeze);
        }

        // Re-register all synced cards on hardware with extended EndDate
        await ReEnableCardsOnHardware(employee);

        await LogAuditAsync("Unfreeze", "Player", id,
            $"Unfroze player: {employee.FullNameEn} ({employee.CardNo}). Freeze duration: {freezeDays} days. EndDate extended to {employee.EndDate:yyyy-MM-dd}",
            $"تم إلغاء تجميد لاعب: {employee.FullNameAr} ({employee.CardNo}). مدة التجميد: {freezeDays} يوم. تاريخ الانتهاء الجديد: {employee.EndDate:yyyy-MM-dd}");

        // Log to session file
        await _sessionLogger.LogOperationAsync("UNFREEZE", "Player", id,
            $"Unfroze player: {employee.FullNameEn} ({employee.CardNo}). Freeze duration: {freezeDays} days",
            $"تم إلغاء تجميد لاعب: {employee.FullNameAr} ({employee.CardNo}). مدة التجميد: {freezeDays} يوم",
            _currentUser.Username);

        return true;
    }

    public async Task<bool> RenewSubscriptionAsync(int id, string subscriptionType, int months, int customDays,
        decimal fee, decimal amountPaid, string doorPermissions, int effectiveTimes)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        if (employee == null) return false;

        var oldType = employee.SubscriptionType;
        var oldEndDate = employee.EndDate;

        employee.SubscriptionType = subscriptionType;
        employee.StartDate = DateTime.Today;
        employee.EndDate = customDays > 0
            ? DateTime.Today.AddDays(customDays)
            : DateTime.Today.AddMonths(months);
        if (employee.EndDate <= employee.StartDate)
            throw new ArgumentException("End date must be after start date.");
        employee.SubscriptionFee = fee;
        employee.AmountPaid = amountPaid;
        employee.IsFrozen = false;
        employee.FreezeStartDate = null;
        await _employeeRepository.UpdateAsync(employee);

        // Update all cards with new door permissions, effective times, and validity
        if (employee.AccessCards != null)
        {
            foreach (var card in employee.AccessCards)
            {
                card.DoorPermissions = doorPermissions;
                card.EffectiveTimes = effectiveTimes;
                card.ValidFrom = employee.StartDate;
                card.ValidTo = employee.EndDate;
                card.IsActive = true;
                await _cardRepository.UpdateAsync(card);
            }
        }

        // Auto-create income transaction for subscription payment
        if (amountPaid > 0)
        {
            await _transactionRepository.AddAsync(new Transaction
            {
                Type = TransactionType.Income,
                Category = "Subscription",
                Amount = amountPaid,
                Description = $"Subscription renewal: {employee.FullNameEn} ({subscriptionType})",
                RelatedEmployeeId = id,
                PaymentMethod = PaymentMethod.Cash,
                CreatedBy = _currentUser.Username ?? "System"
            });
        }

        var periodLabel = customDays > 0 ? $"{customDays} days" : $"{months} months";
        var periodLabelAr = customDays > 0 ? $"{customDays} يوم" : $"{months} شهر";

        await LogAuditAsync("Renew", "Player", id,
            $"Renewed subscription for {employee.FullNameEn} ({employee.CardNo}). " +
            $"Type: {oldType} → {subscriptionType}. " +
            $"Period: {periodLabel}. Fee: {fee}. Paid: {amountPaid}. " +
            $"Old EndDate: {oldEndDate:yyyy-MM-dd} → New EndDate: {employee.EndDate:yyyy-MM-dd}",
            $"تم تجديد اشتراك {employee.FullNameAr} ({employee.CardNo}). " +
            $"النوع: {oldType} → {subscriptionType}. " +
            $"المدة: {periodLabelAr}. الرسوم: {fee}. المدفوع: {amountPaid}. " +
            $"تاريخ الانتهاء القديم: {oldEndDate:yyyy-MM-dd} → الجديد: {employee.EndDate:yyyy-MM-dd}");

        // Log to session file
        await _sessionLogger.LogOperationAsync("RENEW", "Player", id,
            $"Renewed subscription for {employee.FullNameEn} from {oldEndDate:yyyy-MM-dd} to {employee.EndDate:yyyy-MM-dd}. Amount: {amountPaid}",
            $"تم تجديد اشتراك {employee.FullNameAr} من {oldEndDate:yyyy-MM-dd} إلى {employee.EndDate:yyyy-MM-dd}. المبلغ: {amountPaid}",
            _currentUser.Username);

        return true;
    }

    public async Task<(int success, int failed)> BulkFreezeAsync(IEnumerable<int> ids, string reason)
    {
        int success = 0, failed = 0;
        foreach (var id in ids)
        {
            try
            {
                if (await FreezePlayerAsync(id, reason))
                    success++;
                else
                    failed++;
            }
            catch { failed++; }
        }
        return (success, failed);
    }

    public async Task<(int success, int failed)> BulkUnfreezeAsync(IEnumerable<int> ids)
    {
        int success = 0, failed = 0;
        foreach (var id in ids)
        {
            try
            {
                if (await UnfreezePlayerAsync(id))
                    success++;
                else
                    failed++;
            }
            catch { failed++; }
        }
        return (success, failed);
    }

    public async Task<(int success, int failed)> BulkExtendAsync(IEnumerable<int> ids, int days)
    {
        int success = 0, failed = 0;
        foreach (var id in ids)
        {
            try
            {
                var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
                if (employee == null) { failed++; continue; }

                var oldEnd = employee.EndDate;
                employee.EndDate = employee.EndDate.AddDays(days);

                // Also extend card validity
                if (employee.AccessCards != null)
                {
                    foreach (var card in employee.AccessCards)
                    {
                        card.ValidTo = employee.EndDate;
                        await _cardRepository.UpdateAsync(card);
                    }
                }

                await _employeeRepository.UpdateAsync(employee);
                await LogAuditAsync("BulkExtend", "Player", id,
                    $"Extended {employee.FullNameEn} by {days} days. EndDate: {oldEnd:yyyy-MM-dd} → {employee.EndDate:yyyy-MM-dd}",
                    $"تم تمديد اشتراك {employee.FullNameAr} بمقدار {days} يوم. {oldEnd:yyyy-MM-dd} → {employee.EndDate:yyyy-MM-dd}");
                success++;
            }
            catch { failed++; }
        }
        return (success, failed);
    }

    public async Task<PlayerProfileDto> GetPlayerProfileAsync(int id)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id)
            ?? throw new InvalidOperationException("Player not found");

        var profile = new PlayerProfileDto
        {
            Player = ToDto(employee)
        };

        // Freeze history
        var freezes = await _freezeHistoryRepository.GetByEmployeeIdAsync(id);
        profile.FreezeHistory = freezes.Select(f => new FreezeHistoryDto
        {
            FreezeStart = f.FreezeStart,
            FreezeEnd = f.FreezeEnd,
            FreezeDays = f.FreezeDays,
            Reason = f.Reason
        }).ToList();

        // Transactions
        var transactions = await _transactionRepository.GetByEmployeeIdAsync(id);
        profile.Transactions = transactions.Select(t => new TransactionDto
        {
            Id = t.Id,
            Type = t.Type,
            Category = t.Category,
            Amount = t.Amount,
            Description = t.Description,
            PaymentMethod = t.PaymentMethod,
            CreatedBy = t.CreatedBy,
            CreatedAt = t.CreatedAt
        }).ToList();

        // Audit logs
        var logs = await _auditLogRepository.GetByEntityAsync("Player", id);
        profile.AuditLogs = logs.Select(l => new AuditLogDto
        {
            Action = l.Action,
            Details = l.Details,
            DetailsAr = l.DetailsAr,
            PerformedBy = l.PerformedBy,
            Timestamp = l.Timestamp
        }).ToList();

        return profile;
    }

    public async Task<bool> AssignCardAsync(int employeeId, AccessCardDto cardDto)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(employeeId);
        if (employee == null) return false;

        var existingCard = await _cardRepository.GetByCardNumberAsync(cardDto.CardNumber);
        if (existingCard != null)
            throw new InvalidOperationException($"Card number '{cardDto.CardNumber}' is already assigned.");

        var card = new AccessCard
        {
            EmployeeId = employeeId,
            CardNumber = cardDto.CardNumber,
            CardPassword = cardDto.CardPassword,
            CardType = cardDto.CardType,
            OpenMode = cardDto.OpenMode,
            DoorPermissions = cardDto.DoorPermissions,
            EffectiveTimes = cardDto.EffectiveTimes,
            TimePeriodIndex = cardDto.TimePeriodIndex,
            HolidayEnabled = cardDto.HolidayEnabled,
            IsActive = cardDto.IsActive,
            ValidFrom = cardDto.ValidFrom,
            ValidTo = cardDto.ValidTo
        };
        try
        {
            await _cardRepository.AddAsync(card);
        }
        catch (Exception ex) when (ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true
                                || ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
                                || ex.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                                || ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Card number '{cardDto.CardNumber}' is already assigned to another player.");
        }
        await LogAuditAsync("Create", "AccessCard", card.Id,
            $"Assigned card {cardDto.CardNumber} to {employee.FullNameEn}",
            $"تم تعيين بطاقة {cardDto.CardNumber} إلى {employee.FullNameAr}");

        // Log to session file
        await _sessionLogger.LogOperationAsync("ASSIGN_CARD", "AccessCard", card.Id,
            $"Assigned card {cardDto.CardNumber} to player {employee.FullNameEn}",
            $"تم تعيين بطاقة {cardDto.CardNumber} للاعب {employee.FullNameAr}",
            _currentUser.Username);

        return true;
    }

    public async Task<bool> RemoveCardAsync(int cardId)
    {
        var card = await _cardRepository.GetByIdAsync(cardId);
        if (card == null) return false;

        await LogAuditAsync("Delete", "AccessCard", cardId,
            $"Removed card {card.CardNumber} from player {card.EmployeeId}",
            $"تم إزالة بطاقة {card.CardNumber} من اللاعب {card.EmployeeId}");

        // Log to session file
        await _sessionLogger.LogOperationAsync("REMOVE_CARD", "AccessCard", cardId,
            $"Removed card {card.CardNumber} from player ID {card.EmployeeId}",
            $"تم إزالة بطاقة {card.CardNumber} من اللاعب ID {card.EmployeeId}",
            _currentUser.Username);

        await _cardRepository.DeleteAsync(cardId);
        return true;
    }

    public async Task<bool> SyncCardToDeviceAsync(int cardId, int deviceId)
    {
        var card = await _cardRepository.GetByIdAsync(cardId);
        if (card == null)
            throw new InvalidOperationException($"Card with ID {cardId} not found in database.");

        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null)
            throw new InvalidOperationException($"Device with ID {deviceId} not found in database.");

        try
        {
            _sdk.Initialize();

            var deviceInfo = new DeviceInfo
            {
                IP = device.IP,
                MAC = device.MAC,
                SerialNumber = device.SerialNumber,
                TCPPort = device.TCPPort,
                Password = device.Password,
                Gateway = device.Gateway,
                SubnetMask = device.SubnetMask
            };

            var permitTime = card.ValidTo > DateTime.MinValue ?
                card.ValidTo.ToString("yyyy-MM-dd HH:mm:ss") :
                DateTime.UtcNow.AddYears(10).ToString("yyyy-MM-dd HH:mm:ss");

            _sdk.AddAccessCard(
                deviceInfo,
                card.CardNumber,
                card.CardPassword,
                card.OpenMode,
                card.DoorPermissions,
                permitTime,
                card.EffectiveTimes,
                card.TimePeriodIndex,
                card.HolidayEnabled);

            card.IsSyncedToDevice = true;
            await _cardRepository.UpdateAsync(card);

            // Track per-device sync status
            await _cardDeviceSyncRepository.UpsertAsync(cardId, deviceId, true);

            await LogAuditAsync("SyncCard", "AccessCard", cardId,
                $"Synced card {card.CardNumber} to device {device.Name} ({device.IP})",
                $"تم مزامنة بطاقة {card.CardNumber} مع جهاز {device.Name} ({device.IP})");

            // Log to session file
            await _sessionLogger.LogOperationAsync("SYNC_CARD", "AccessCard", cardId,
                $"Successfully synced card {card.CardNumber} to device {device.Name}",
                $"تمت مزامنة بطاقة {card.CardNumber} مع جهاز {device.Name} بنجاح",
                _currentUser.Username);

            return true;
        }
        catch (Exception ex)
        {
            // Track failed sync
            await _cardDeviceSyncRepository.UpsertAsync(cardId, deviceId, false, ex.Message);

            // Log error to session file
            await _sessionLogger.LogErrorAsync("SYNC_CARD", "AccessCard", cardId,
                $"Failed to sync card {card.CardNumber} to device {device.Name}: {ex.Message}",
                _currentUser.Username);

            var errorMsg = ex.Message;

            // Check if player exists in database but not in hardware
            if (errorMsg.Contains("card") || errorMsg.Contains("not found") || errorMsg.Contains("Communication"))
            {
                throw new InvalidOperationException(
                    $"Failed to sync card {card.CardNumber} to device {device.Name}. " +
                    $"The player may be deleted from the device or device is unreachable. " +
                    $"Error: {errorMsg}");
            }

            // Device unreachable
            if (errorMsg.Contains("timeout") || errorMsg.Contains("Timeout") ||
                errorMsg.Contains("connect") || errorMsg.Contains("Connect"))
            {
                throw new InvalidOperationException(
                    $"Cannot reach device {device.Name} at {device.IP}:{device.TCPPort}. " +
                    $"Please check if the device is connected to the network.");
            }

            // Generic error
            throw new InvalidOperationException(
                $"Error syncing card {card.CardNumber} to device {device.Name}: {errorMsg}", ex);
        }
    }

    public async Task<(int synced, int failed, int total)> SyncAllCardsToDeviceAsync(
        int deviceId, IProgress<(int current, int total, string cardNumber)>? progress = null)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return (0, 0, 0);

        var allCards = (await _cardRepository.GetAllWithEmployeeAsync())
            .Where(c => c.IsActive && c.Employee != null)
            .ToList();

        if (allCards.Count == 0) return (0, 0, 0);

        _sdk.Initialize();
        var deviceInfo = BuildDeviceInfo(device);

        int synced = 0, failed = 0;
        for (int i = 0; i < allCards.Count; i++)
        {
            var card = allCards[i];
            progress?.Report((i + 1, allCards.Count, card.CardNumber));

            try
            {
                var permitTime = card.ValidTo.ToString("yyyy-MM-dd HH:mm:ss");
                _sdk.AddAccessCard(
                    deviceInfo,
                    card.CardNumber,
                    card.CardPassword,
                    card.OpenMode,
                    card.DoorPermissions,
                    permitTime,
                    card.EffectiveTimes,
                    card.TimePeriodIndex,
                    card.HolidayEnabled);

                card.IsSyncedToDevice = true;
                await _cardRepository.UpdateAsync(card);
                synced++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SyncAll] Failed card {card.CardNumber}: {ex.Message}");
                failed++;
            }
        }

        await LogAuditAsync("SyncAllCards", "Device", deviceId,
            $"Bulk synced {synced}/{allCards.Count} cards to device {device.Name} ({device.IP}). Failed: {failed}",
            $"تم مزامنة {synced}/{allCards.Count} بطاقة مع جهاز {device.Name} ({device.IP}). فشل: {failed}");

        return (synced, failed, allCards.Count);
    }

    public async Task<(int synced, int failed, int total, List<string> errors)> SyncCardToAllDevicesAsync(int cardId)
    {
        var card = await _cardRepository.GetByIdAsync(cardId);
        if (card == null)
            throw new InvalidOperationException($"Card with ID {cardId} not found.");

        var devices = (await _deviceRepository.GetAllAsync()).ToList();
        if (devices.Count == 0)
            return (0, 0, 0, new List<string> { "No devices found." });

        _sdk.Initialize();
        int synced = 0, failed = 0;
        var errors = new List<string>();

        var permitTime = card.ValidTo > DateTime.MinValue
            ? card.ValidTo.ToString("yyyy-MM-dd HH:mm:ss")
            : DateTime.UtcNow.AddYears(10).ToString("yyyy-MM-dd HH:mm:ss");

        foreach (var device in devices)
        {
            try
            {
                _sdk.AddAccessCard(
                    BuildDeviceInfo(device),
                    card.CardNumber,
                    card.CardPassword,
                    card.OpenMode,
                    card.DoorPermissions,
                    permitTime,
                    card.EffectiveTimes,
                    card.TimePeriodIndex,
                    card.HolidayEnabled);

                await _cardDeviceSyncRepository.UpsertAsync(cardId, device.Id, true);
                synced++;
            }
            catch (Exception ex)
            {
                await _cardDeviceSyncRepository.UpsertAsync(cardId, device.Id, false, ex.Message);
                errors.Add($"{device.Name} ({device.IP}): {ex.Message}");
                failed++;
            }
        }

        card.IsSyncedToDevice = synced > 0;
        await _cardRepository.UpdateAsync(card);

        await LogAuditAsync("SyncCardAllDevices", "AccessCard", cardId,
            $"Synced card {card.CardNumber} to {synced}/{devices.Count} devices. Failed: {failed}",
            $"تم مزامنة بطاقة {card.CardNumber} مع {synced}/{devices.Count} جهاز. فشل: {failed}");

        return (synced, failed, devices.Count, errors);
    }

    public async Task<(int synced, int failed, int total)> SyncAllCardsToAllDevicesAsync(
        IProgress<(int current, int total, string cardNumber)>? progress = null)
    {
        var devices = (await _deviceRepository.GetAllAsync()).ToList();
        if (devices.Count == 0) return (0, 0, 0);

        var allCards = (await _cardRepository.GetAllWithEmployeeAsync())
            .Where(c => c.IsActive && c.Employee != null)
            .ToList();

        if (allCards.Count == 0) return (0, 0, 0);

        _sdk.Initialize();
        int synced = 0, failed = 0;
        int totalOps = allCards.Count * devices.Count;
        int current = 0;

        foreach (var card in allCards)
        {
            var permitTime = card.ValidTo > DateTime.MinValue
                ? card.ValidTo.ToString("yyyy-MM-dd HH:mm:ss")
                : DateTime.UtcNow.AddYears(10).ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var device in devices)
            {
                current++;
                progress?.Report((current, totalOps, card.CardNumber));

                try
                {
                    _sdk.AddAccessCard(
                        BuildDeviceInfo(device),
                        card.CardNumber,
                        card.CardPassword,
                        card.OpenMode,
                        card.DoorPermissions,
                        permitTime,
                        card.EffectiveTimes,
                        card.TimePeriodIndex,
                        card.HolidayEnabled);

                    await _cardDeviceSyncRepository.UpsertAsync(card.Id, device.Id, true);
                    synced++;
                }
                catch (Exception ex)
                {
                    await _cardDeviceSyncRepository.UpsertAsync(card.Id, device.Id, false, ex.Message);
                    failed++;
                }
            }

            card.IsSyncedToDevice = true;
            await _cardRepository.UpdateAsync(card);
        }

        await LogAuditAsync("SyncAllCardsAllDevices", "System", null,
            $"Bulk synced {synced}/{totalOps} card-device pairs ({allCards.Count} cards × {devices.Count} devices). Failed: {failed}",
            $"تم مزامنة {synced}/{totalOps} بطاقة-جهاز ({allCards.Count} بطاقة × {devices.Count} جهاز). فشل: {failed}");

        return (synced, failed, totalOps);
    }

    public async Task<(int synced, int failed, int total, List<string> errors)> RemoveCardFromAllDevicesAsync(int cardId)
    {
        var card = await _cardRepository.GetByIdAsync(cardId);
        if (card == null)
            throw new InvalidOperationException($"Card with ID {cardId} not found.");

        var devices = (await _deviceRepository.GetAllAsync()).ToList();
        if (devices.Count == 0)
            return (0, 0, 0, new List<string>());

        _sdk.Initialize();
        int synced = 0, failed = 0;
        var errors = new List<string>();

        foreach (var device in devices)
        {
            try
            {
                // SDK has no delete function — expire the card by setting date far in past
                _sdk.AddAccessCard(
                    BuildDeviceInfo(device),
                    card.CardNumber,
                    card.CardPassword,
                    card.OpenMode,
                    card.DoorPermissions,
                    "2000-01-01 00:00:00",
                    card.EffectiveTimes,
                    card.TimePeriodIndex,
                    card.HolidayEnabled);
                synced++;
            }
            catch (Exception ex)
            {
                errors.Add($"{device.Name} ({device.IP}): {ex.Message}");
                failed++;
            }
        }

        // Clean up sync tracking records
        await _cardDeviceSyncRepository.DeleteByCardIdAsync(cardId);

        card.IsSyncedToDevice = false;
        await _cardRepository.UpdateAsync(card);

        return (synced, failed, devices.Count, errors);
    }

    public async Task IncrementVisitAsync(string cardNumber)
    {
        var card = await _cardRepository.GetByCardNumberAsync(cardNumber);
        if (card?.Employee == null) return;

        var employee = card.Employee;
        if (employee.MaxVisits <= 0) return; // Date-based only, no visit tracking

        employee.UsedVisits++;
        await _employeeRepository.UpdateAsync(employee);

        // Check if visits exhausted
        if (employee.UsedVisits >= employee.MaxVisits)
        {
            // Auto-expire: disable card on all devices
            await DisableCardsOnHardware(employee);
            await LogAuditAsync("VisitLimitReached", "Employee", employee.Id,
                $"Player {employee.FullNameEn} reached visit limit ({employee.UsedVisits}/{employee.MaxVisits}). Cards disabled on all devices.",
                $"اللاعب {employee.FullNameAr} وصل حد الزيارات ({employee.UsedVisits}/{employee.MaxVisits}). تم تعطيل البطاقات.");
        }
    }

    public async Task<int> GetCountAsync()
        => await _employeeRepository.GetCountAsync();

    public async Task<bool> IsCardNoDuplicateAsync(string cardNo, int? excludeId = null)
    {
        var existing = await _employeeRepository.GetByEmployeeCodeAsync(cardNo);
        if (existing == null) return false;
        return excludeId == null || existing.Id != excludeId.Value;
    }

    public async Task<bool> IsPhoneDuplicateAsync(string phone, int? excludeId = null)
    {
        var existing = await _employeeRepository.GetByPhoneAsync(phone);
        if (existing == null) return false;
        return excludeId == null || existing.Id != excludeId.Value;
    }

    public async Task<bool> IsNameDuplicateAsync(string fullNameEn, int? excludeId = null)
    {
        return await _employeeRepository.ExistsByNameAsync(fullNameEn, excludeId);
    }

    private static DeviceInfo BuildDeviceInfo(Device device) => new()
    {
        IP = device.IP,
        MAC = device.MAC,
        SerialNumber = device.SerialNumber,
        TCPPort = device.TCPPort,
        Password = device.Password,
        Gateway = device.Gateway,
        SubnetMask = device.SubnetMask
    };

    private async Task DisableCardsOnHardware(Employee employee)
    {
        var syncedCards = employee.AccessCards?.Where(c => c.IsActive && c.IsSyncedToDevice).ToList();
        if (syncedCards == null || syncedCards.Count == 0) return;

        _sdk.Initialize();
        var devices = (await _deviceRepository.GetAllAsync()).ToList();

        foreach (var card in syncedCards)
        {
            foreach (var device in devices)
            {
                try
                {
                    _sdk.AddAccessCard(
                        BuildDeviceInfo(device),
                        card.CardNumber,
                        card.CardPassword,
                        card.OpenMode,
                        card.DoorPermissions,
                        "2000-01-01 00:00:00",
                        card.EffectiveTimes,
                        card.TimePeriodIndex,
                        card.HolidayEnabled);

                    await _cardDeviceSyncRepository.UpsertAsync(card.Id, device.Id, true);
                }
                catch (Exception ex)
                {
                    await _cardDeviceSyncRepository.UpsertAsync(card.Id, device.Id, false, ex.Message);
                    System.Diagnostics.Debug.WriteLine($"[EmployeeService] Failed to disable card {card.CardNumber} on device {device.IP}: {ex.Message}");
                }
            }
        }
    }

    private async Task ReEnableCardsOnHardware(Employee employee)
    {
        var syncedCards = employee.AccessCards?.Where(c => c.IsActive && c.IsSyncedToDevice).ToList();
        if (syncedCards == null || syncedCards.Count == 0) return;

        _sdk.Initialize();
        var devices = (await _deviceRepository.GetAllAsync()).ToList();
        var newPermitTime = employee.EndDate.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (var card in syncedCards)
        {
            card.ValidTo = employee.EndDate;
            await _cardRepository.UpdateAsync(card);

            foreach (var device in devices)
            {
                try
                {
                    _sdk.AddAccessCard(
                        BuildDeviceInfo(device),
                        card.CardNumber,
                        card.CardPassword,
                        card.OpenMode,
                        card.DoorPermissions,
                        newPermitTime,
                        card.EffectiveTimes,
                        card.TimePeriodIndex,
                        card.HolidayEnabled);

                    await _cardDeviceSyncRepository.UpsertAsync(card.Id, device.Id, true);
                }
                catch (Exception ex)
                {
                    await _cardDeviceSyncRepository.UpsertAsync(card.Id, device.Id, false, ex.Message);
                    System.Diagnostics.Debug.WriteLine($"[EmployeeService] Failed to re-enable card {card.CardNumber} on device {device.IP}: {ex.Message}");
                }
            }
        }
    }

    private async Task LogAuditAsync(string action, string entityType, int? entityId, string details, string detailsAr)
    {
        await _auditLogRepository.AddAsync(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            DetailsAr = detailsAr,
            PerformedBy = _currentUser.Username ?? "System",
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task<IEnumerable<EmployeeDto>> GetExpiringAsync(DateTime from, DateTime to)
    {
        var employees = await _employeeRepository.GetBySubscriptionEndDateRangeAsync(from, to);
        return employees.Select(ToDto);
    }

    public async Task<IEnumerable<EmployeeDto>> GetRenewedAsync(DateTime from, DateTime to)
    {
        var employees = await _employeeRepository.GetByStartDateRangeAsync(from, to);
        return employees.Select(ToDto);
    }

    public async Task<IEnumerable<EmployeeDto>> GetFrozenPlayersAsync()
    {
        var employees = await _employeeRepository.GetFrozenAsync();
        return employees.Select(ToDto);
    }

    public async Task<IEnumerable<EmployeeDto>> GetExpiredPlayersAsync()
    {
        var employees = await _employeeRepository.GetExpiredAsync();
        return employees.Select(ToDto);
    }

    private static EmployeeDto ToDto(Employee e) => new()
    {
        Id = e.Id,
        FullNameEn = e.FullNameEn,
        FullNameAr = e.FullNameAr,
        CardNo = e.CardNo,
        SubscriptionType = e.SubscriptionType,
        Phone = e.Phone,
        PhotoData = e.PhotoData,
        Height = e.Height,
        Weight = e.Weight,
        SubscriptionFee = e.SubscriptionFee,
        AmountPaid = e.AmountPaid,
        StartDate = e.StartDate,
        EndDate = e.EndDate,
        Notes = e.Notes,
        IsFrozen = e.IsFrozen,
        FreezeStartDate = e.FreezeStartDate,
        CardBalance = e.CardBalance,
        MaxVisits = e.MaxVisits,
        UsedVisits = e.UsedVisits,
        CardCount = e.AccessCards?.Count ?? 0,
        CreatedAt = e.CreatedAt,
        Cards = (e.AccessCards ?? []).Select(c => new AccessCardDto
        {
            Id = c.Id,
            EmployeeId = c.EmployeeId,
            EmployeeName = e.FullNameEn,
            CardNumber = c.CardNumber,
            CardPassword = c.CardPassword,
            CardType = c.CardType,
            OpenMode = c.OpenMode,
            DoorPermissions = c.DoorPermissions,
            EffectiveTimes = c.EffectiveTimes,
            TimePeriodIndex = c.TimePeriodIndex,
            HolidayEnabled = c.HolidayEnabled,
            IsActive = c.IsActive,
            IsSyncedToDevice = c.IsSyncedToDevice,
            ValidFrom = c.ValidFrom,
            ValidTo = c.ValidTo
        }).ToList()
    };
}
