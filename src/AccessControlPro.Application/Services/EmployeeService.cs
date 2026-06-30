using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Helpers;
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
    private readonly IAccessEventRepository _accessEventRepository;
    private readonly CurrentUserService _currentUser;
    private readonly ISessionLogger _sessionLogger;
    private readonly DeviceOperationHelper _opHelper;
    private readonly IQrPoolService _qrPool;

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
        IAccessEventRepository accessEventRepository,
        CurrentUserService currentUser,
        ISessionLogger sessionLogger,
        DeviceOperationHelper opHelper,
        IQrPoolService qrPool)
    {
        _qrPool = qrPool;
        _employeeRepository = employeeRepository;
        _cardRepository = cardRepository;
        _deviceRepository = deviceRepository;
        _sdk = sdk;
        _auditLogRepository = auditLogRepository;
        _deletedEmployeeRepository = deletedEmployeeRepository;
        _freezeHistoryRepository = freezeHistoryRepository;
        _transactionRepository = transactionRepository;
        _cardDeviceSyncRepository = cardDeviceSyncRepository;
        _accessEventRepository = accessEventRepository;
        _currentUser = currentUser;
        _sessionLogger = sessionLogger;
        _opHelper = opHelper;
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
        // At least one name (EN or AR) is required; if one is empty, copy the other
        if (string.IsNullOrWhiteSpace(dto.FullNameEn) && string.IsNullOrWhiteSpace(dto.FullNameAr))
            throw new ArgumentException("At least one name (English or Arabic) is required.");
        if (string.IsNullOrWhiteSpace(dto.FullNameEn))
            dto.FullNameEn = dto.FullNameAr;
        if (string.IsNullOrWhiteSpace(dto.FullNameAr))
            dto.FullNameAr = dto.FullNameEn;
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
            Notes = dto.Notes,
            MaxVisits = dto.MaxVisits,
            UsedVisits = 0
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

        // At least one name required; copy if one is empty
        if (string.IsNullOrWhiteSpace(dto.FullNameEn) && string.IsNullOrWhiteSpace(dto.FullNameAr))
            throw new ArgumentException("At least one name (English or Arabic) is required.");
        if (string.IsNullOrWhiteSpace(dto.FullNameEn))
            dto.FullNameEn = dto.FullNameAr;
        if (string.IsNullOrWhiteSpace(dto.FullNameAr))
            dto.FullNameAr = dto.FullNameEn;

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
        if (employee.MaxVisits != dto.MaxVisits)
        {
            changesEn.Add($"MaxVisits: {employee.MaxVisits} → {dto.MaxVisits}");
            changesAr.Add($"الحد الأقصى للزيارات: {employee.MaxVisits} → {dto.MaxVisits}");
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
        employee.MaxVisits = dto.MaxVisits;
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
            // Step 1: Archive to DeletedEmployees table (preserves all player data)
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

            // Step 2: Try to remove cards from hardware (best-effort — don't block delete if device offline)
            string? hardwareWarning = null;
            var cards = employee.AccessCards?.ToList() ?? new List<AccessCard>();
            if (cards.Count > 0)
            {
                try
                {
                    await DisableCardsOnHardware(employee);
                }
                catch (Exception hwEx)
                {
                    hardwareWarning = hwEx.Message;
                    await _sessionLogger.LogErrorAsync("SOFT_DELETE", "Player", id,
                        $"Card removal from hardware failed (player will still be deleted): {hwEx.Message}",
                        _currentUser.Username);
                }

                // Step 3: Clear FK references before deleting employee
                var cardIds = cards.Select(c => c.Id).ToList();
                await _accessEventRepository.NullifyCardIdForCardsAsync(cardIds);

                foreach (var card in cards)
                {
                    await _cardDeviceSyncRepository.DeleteByCardIdAsync(card.Id);
                }
            }

            // Step 4: Nullify Transactions.RelatedEmployeeId (preserves financial records)
            await _transactionRepository.NullifyEmployeeIdAsync(id);

            // Step 5: Audit + session log
            var logNote = hardwareWarning != null ? " [Hardware: card not removed from device]" : "";
            await LogAuditAsync("SoftDelete", "Player", id,
                $"Soft-deleted player: {employee.FullNameEn} ({employee.CardNo}). Reason: {reason}{logNote}",
                $"تم حذف لاعب: {employee.FullNameAr} ({employee.CardNo}). السبب: {reason}{logNote}");

            await _sessionLogger.LogOperationAsync("SOFT_DELETE", "Player", id,
                $"Soft-deleted player: {employee.FullNameEn} ({employee.CardNo}). Reason: {reason}{logNote}",
                $"تم حذف لاعب: {employee.FullNameAr} ({employee.CardNo}). السبب: {reason}{logNote}",
                _currentUser.Username);

            // Step 6: Delete employee from active table
            await _employeeRepository.DeleteAsync(id);

            // Return warning info if hardware removal failed
            if (hardwareWarning != null)
                throw new InvalidOperationException(
                    $"PARTIAL_SUCCESS:Player deleted from system successfully, but card could not be removed from hardware device. Please remove card manually from device settings.");

            return true;
        }
        catch (Exception ex)
        {
            await _sessionLogger.LogErrorAsync("SOFT_DELETE", "Player", id,
                $"Failed to soft-delete player. Error: {ex.Message}",
                _currentUser.Username);
            throw;
        }
    }

    public async Task<bool> FreezePlayerAsync(int id, string reason, IEnumerable<int>? deviceIds = null)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        if (employee == null || employee.IsFrozen) return false;

        // Step 1: Disable cards on hardware FIRST (best-effort — don't block freeze if device offline)
        string? hardwareWarning = null;
        try
        {
            await DisableCardsOnHardware(employee, deviceIds);
        }
        catch (Exception hwEx)
        {
            hardwareWarning = hwEx.Message;
            await _sessionLogger.LogErrorAsync("FREEZE", "Player", id,
                $"Card disable on hardware failed (player will still be frozen in DB): {hwEx.Message}",
                _currentUser.Username);
        }

        // Step 2: Update DB (freeze always happens — hardware might just be offline)
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

        var logNote = hardwareWarning != null ? " [Hardware: card not disabled on device]" : "";
        await LogAuditAsync("Freeze", "Player", id,
            $"Froze player: {employee.FullNameEn} ({employee.CardNo}). Reason: {reason}{logNote}",
            $"تم تجميد لاعب: {employee.FullNameAr} ({employee.CardNo}). السبب: {reason}{logNote}");

        // Log to session file
        await _sessionLogger.LogOperationAsync("FREEZE", "Player", id,
            $"Froze player: {employee.FullNameEn} ({employee.CardNo}). Reason: {reason}{logNote}",
            $"تم تجميد لاعب: {employee.FullNameAr} ({employee.CardNo}). السبب: {reason}{logNote}",
            _currentUser.Username);

        // Surface hardware warning via PARTIAL_SUCCESS so ViewModel can display it
        if (hardwareWarning != null)
            throw new InvalidOperationException(
                $"PARTIAL_SUCCESS:Player frozen in system successfully, but card could not be disabled on hardware device. Please check device connectivity.");

        return true;
    }

    public async Task<bool> UnfreezePlayerAsync(int id, IEnumerable<int>? deviceIds = null)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        if (employee == null || !employee.IsFrozen || employee.FreezeStartDate == null) return false;

        var freezeDays = (int)Math.Ceiling((DateTime.UtcNow - employee.FreezeStartDate.Value).TotalDays);
        if (freezeDays < 1) freezeDays = 1;

        // Calculate new EndDate — give back frozen days
        var newEndDate = employee.EndDate.AddDays(freezeDays);

        // Check if subscription is still valid after extension
        bool subscriptionExpired = newEndDate < DateTime.Now;
        bool visitsExhausted = employee.MaxVisits > 0 && employee.UsedVisits >= employee.MaxVisits;

        // Step 1: Re-enable cards on hardware FIRST (only if subscription is still valid)
        string? hardwareWarning = null;
        if (!subscriptionExpired && !visitsExhausted)
        {
            try
            {
                // Calculate remaining effectiveTimes for hardware
                int remainingEffectiveTimes = 65535; // default unlimited
                if (employee.MaxVisits > 0)
                {
                    var remainingVisits = employee.MaxVisits - employee.UsedVisits;
                    remainingEffectiveTimes = remainingVisits;
                    if (remainingEffectiveTimes < 1) remainingEffectiveTimes = 1;
                }

                await ReEnableCardsOnHardware(employee, deviceIds, newEndDate, remainingEffectiveTimes);
            }
            catch (Exception hwEx)
            {
                hardwareWarning = hwEx.Message;
                await _sessionLogger.LogErrorAsync("UNFREEZE", "Player", id,
                    $"Card re-enable on hardware failed (player will still be unfrozen in DB): {hwEx.Message}",
                    _currentUser.Username);
            }
        }
        else if (subscriptionExpired)
        {
            hardwareWarning = "Subscription already expired — card not re-enabled on hardware";
            await _sessionLogger.LogErrorAsync("UNFREEZE", "Player", id,
                $"Subscription expired (EndDate={newEndDate:yyyy-MM-dd}). Card not re-enabled.",
                _currentUser.Username);
        }
        else if (visitsExhausted)
        {
            hardwareWarning = "No visits remaining — card not re-enabled on hardware";
            await _sessionLogger.LogErrorAsync("UNFREEZE", "Player", id,
                $"No visits remaining ({employee.UsedVisits}/{employee.MaxVisits}). Card not re-enabled.",
                _currentUser.Username);
        }

        // Step 2: Update DB (unfreeze always happens — hardware might just be offline)
        employee.EndDate = newEndDate;
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

        var logNote = hardwareWarning != null ? " [Hardware: card not re-enabled on device]" : "";
        await LogAuditAsync("Unfreeze", "Player", id,
            $"Unfroze player: {employee.FullNameEn} ({employee.CardNo}). Freeze duration: {freezeDays} days. EndDate extended to {employee.EndDate:yyyy-MM-dd}{logNote}",
            $"تم إلغاء تجميد لاعب: {employee.FullNameAr} ({employee.CardNo}). مدة التجميد: {freezeDays} يوم. تاريخ الانتهاء الجديد: {employee.EndDate:yyyy-MM-dd}{logNote}");

        // Log to session file
        await _sessionLogger.LogOperationAsync("UNFREEZE", "Player", id,
            $"Unfroze player: {employee.FullNameEn} ({employee.CardNo}). Freeze duration: {freezeDays} days{logNote}",
            $"تم إلغاء تجميد لاعب: {employee.FullNameAr} ({employee.CardNo}). مدة التجميد: {freezeDays} يوم{logNote}",
            _currentUser.Username);

        // Surface hardware warning via PARTIAL_SUCCESS so ViewModel can display it
        if (hardwareWarning != null)
            throw new InvalidOperationException(
                $"PARTIAL_SUCCESS:Player unfrozen in system successfully, but card could not be re-enabled on hardware device. Please check device connectivity.");

        return true;
    }

    public async Task<bool> RenewSubscriptionAsync(int id, string subscriptionType, int months, int customDays,
        decimal fee, decimal amountPaid, string doorPermissions, int effectiveTimes, IEnumerable<int>? deviceIds = null)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        if (employee == null) return false;

        var oldType = employee.SubscriptionType;
        var oldEndDate = employee.EndDate;

        // Calculate new dates (don't persist yet — hardware goes first)
        var newStartDate = DateTime.Today;
        var newEndDate = customDays > 0
            ? DateTime.Today.AddDays(customDays)
            : DateTime.Today.AddMonths(months);
        if (newEndDate <= newStartDate)
            throw new ArgumentException("End date must be after start date.");

        // EffectiveTimes comes directly from Renew dialog
        // Update employee.MaxVisits = EffectiveTimes (stored as-is from dialog)
        var hwEffectiveTimes = effectiveTimes;
        if (hwEffectiveTimes > 0 && hwEffectiveTimes < 65535)
        {
            employee.MaxVisits = hwEffectiveTimes;
            employee.UsedVisits = 0; // Reset visit counter on renewal
        }
        else
        {
            employee.MaxVisits = 0;
            employee.UsedVisits = 0;
        }

        var newPermitTime = newEndDate.ToString("yyyy-MM-dd HH:mm:ss");

        // Step 1: Sync cards to hardware FIRST with new dates and permissions
        string? hardwareWarning = null;
        var cards = employee.AccessCards?.Where(c => c.IsActive).ToList() ?? new List<AccessCard>();
        if (cards.Count > 0)
        {
            var devices = await ResolveDevicesAsync(deviceIds);
            if (devices.Count > 0)
            {
                foreach (var card in cards)
                {
                    var deviceTuples = devices.Select(d => (BuildDeviceInfo(d), d.Name, d.IP, d.Id));

                    var result = await _opHelper.ExecuteOnDevicesSequentialAsync(deviceTuples, deviceInfo =>
                    {
                        _sdk.AddAccessCard(
                            deviceInfo,
                            card.CardNumber,
                            card.CardPassword,
                            card.OpenMode,
                            doorPermissions,
                            newPermitTime,
                            hwEffectiveTimes,
                            card.TimePeriodIndex,
                            card.HolidayEnabled);
                    });

                    foreach (var s in result.Succeeded)
                        await _cardDeviceSyncRepository.UpsertAsync(card.Id, s.DeviceId, true);

                    foreach (var f in result.Failed)
                    {
                        await _cardDeviceSyncRepository.UpsertAsync(card.Id, f.DeviceId, false, f.Error);
                        hardwareWarning ??= $"Some devices could not be updated: {f.Error}";
                    }
                }
            }
        }

        if (hardwareWarning != null)
        {
            await _sessionLogger.LogErrorAsync("RENEW", "Player", id,
                $"Card sync to hardware partially failed during renewal: {hardwareWarning}",
                _currentUser.Username);
        }

        // Step 2: Update DB with new subscription dates, fees, and card settings
        employee.SubscriptionType = subscriptionType;
        employee.StartDate = newStartDate;
        employee.EndDate = newEndDate;
        employee.SubscriptionFee = fee;
        employee.AmountPaid = amountPaid;
        employee.IsFrozen = false;
        employee.FreezeStartDate = null;
        employee.UsedVisits = 0; // Reset visit count on renewal
        await _employeeRepository.UpdateAsync(employee);

        // Update all cards in DB
        if (employee.AccessCards != null)
        {
            foreach (var card in employee.AccessCards)
            {
                card.DoorPermissions = doorPermissions;
                card.EffectiveTimes = hwEffectiveTimes;
                card.ValidFrom = newStartDate;
                card.ValidTo = newEndDate;
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
        var logNote = hardwareWarning != null ? " [Hardware: partial sync failure]" : "";

        await LogAuditAsync("Renew", "Player", id,
            $"Renewed subscription for {employee.FullNameEn} ({employee.CardNo}). " +
            $"Type: {oldType} → {subscriptionType}. " +
            $"Period: {periodLabel}. Fee: {fee}. Paid: {amountPaid}. " +
            $"Old EndDate: {oldEndDate:yyyy-MM-dd} → New EndDate: {employee.EndDate:yyyy-MM-dd}{logNote}",
            $"تم تجديد اشتراك {employee.FullNameAr} ({employee.CardNo}). " +
            $"النوع: {oldType} → {subscriptionType}. " +
            $"المدة: {periodLabelAr}. الرسوم: {fee}. المدفوع: {amountPaid}. " +
            $"تاريخ الانتهاء القديم: {oldEndDate:yyyy-MM-dd} → الجديد: {employee.EndDate:yyyy-MM-dd}{logNote}");

        // Log to session file
        await _sessionLogger.LogOperationAsync("RENEW", "Player", id,
            $"Renewed subscription for {employee.FullNameEn} from {oldEndDate:yyyy-MM-dd} to {employee.EndDate:yyyy-MM-dd}. Amount: {amountPaid}{logNote}",
            $"تم تجديد اشتراك {employee.FullNameAr} من {oldEndDate:yyyy-MM-dd} إلى {employee.EndDate:yyyy-MM-dd}. المبلغ: {amountPaid}{logNote}",
            _currentUser.Username);

        // Surface hardware warning via PARTIAL_SUCCESS so ViewModel can display it
        if (hardwareWarning != null)
            throw new InvalidOperationException(
                $"PARTIAL_SUCCESS:Subscription renewed in system successfully, but some hardware devices could not be updated. Cards will be synced when devices come back online.");

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

        // Capture old card info for re-assignment audit logging
        var oldActiveCard = employee.AccessCards?.FirstOrDefault(c => c.IsActive);
        string? oldCardNo = oldActiveCard?.CardNumber;
        int oldUsedVisits = employee.UsedVisits;
        int oldMaxVisits = employee.MaxVisits;
        bool isReassignment = oldActiveCard != null;

        var existingCard = await _cardRepository.GetByCardNumberAsync(cardDto.CardNumber);
        if (existingCard != null)
        {
            if (existingCard.EmployeeId == employeeId)
            {
                // Same player — update card settings and re-sync to devices
                existingCard.CardPassword = cardDto.CardPassword;
                existingCard.CardType = cardDto.CardType;
                existingCard.OpenMode = cardDto.OpenMode;
                existingCard.DoorPermissions = cardDto.DoorPermissions;
                existingCard.EffectiveTimes = cardDto.EffectiveTimes;
                existingCard.TimePeriodIndex = cardDto.TimePeriodIndex;
                existingCard.HolidayEnabled = cardDto.HolidayEnabled;
                existingCard.ValidFrom = cardDto.ValidFrom;
                existingCard.ValidTo = cardDto.ValidTo;
                existingCard.IsActive = true;
                await _cardRepository.UpdateAsync(existingCard);

                // Log re-assignment if this was a re-assign of the same card
                if (isReassignment)
                {
                    await _sessionLogger.LogOperationAsync("REASSIGN_CARD", "Player", employee.Id,
                        $"Card re-assigned. Old: {oldCardNo} (used {oldUsedVisits}/{oldMaxVisits}). New effectiveTimes: {cardDto.EffectiveTimes}",
                        $"إعادة تعيين البطاقة. القديمة: {oldCardNo} (مستخدم {oldUsedVisits}/{oldMaxVisits}). مرات جديدة: {cardDto.EffectiveTimes}",
                        _currentUser.Username);
                }

                return true;
            }
            throw new InvalidOperationException($"Card number '{cardDto.CardNumber}' is already assigned to another player.");
        }

        // Card priority over the QR pool: a physical card number is fixed in hardware, a pool code is
        // regenerable — so if they collide, release the pool code and let the card take the number.
        var poolRelease = await _qrPool.ReleasePoolCodeAsync(cardDto.CardNumber);
        if (poolRelease.removed)
            await _sessionLogger.LogOperationAsync("CARD_POOL_COLLISION", "Player", employee.Id,
                $"Card {cardDto.CardNumber} matched a QR pool code — pool code released (card priority){(poolRelease.wasAssigned ? "; it was an ACTIVE guest pass" : "")}.",
                $"رقم البطاقة {cardDto.CardNumber} طابق رمز بوول — تم تحرير رمز البوول (أولوية البطاقة){(poolRelease.wasAssigned ? " (كان تذكرة زائر فعّالة)" : "")}.",
                _currentUser.Username);

        // EffectiveTimes comes directly from Assign Card dialog
        // Update employee.MaxVisits = EffectiveTimes (stored as-is from dialog)
        var hwEffectiveTimes = cardDto.EffectiveTimes;
        if (hwEffectiveTimes > 0 && hwEffectiveTimes < 65535)
        {
            employee.MaxVisits = hwEffectiveTimes;
            employee.UsedVisits = 0; // Reset visit counter on new card assignment
            await _employeeRepository.UpdateAsync(employee);
        }
        else
        {
            // Unlimited — clear MaxVisits
            employee.MaxVisits = 0;
            employee.UsedVisits = 0;
            await _employeeRepository.UpdateAsync(employee);
        }

        var card = new AccessCard
        {
            EmployeeId = employeeId,
            CardNumber = cardDto.CardNumber,
            CardPassword = cardDto.CardPassword,
            CardType = cardDto.CardType,
            OpenMode = cardDto.OpenMode,
            DoorPermissions = cardDto.DoorPermissions,
            EffectiveTimes = hwEffectiveTimes,
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

        // Re-assignment to a DIFFERENT number: retire the previous card so the player keeps
        // exactly ONE active card and the old physical card stops opening the gate. (When the
        // number is unchanged, the update-in-place path above runs and we never reach here.)
        if (oldActiveCard != null && oldActiveCard.Id != card.Id)
        {
            try { await RemoveCardFromDevicesAsync(oldActiveCard.Id); } // expire old number on the gate
            catch (Exception ex) { Console.WriteLine($"Retire old card {oldCardNo} on device failed (best-effort): {ex.Message}"); }
            oldActiveCard.IsActive = false;
            await _cardRepository.UpdateAsync(oldActiveCard);
        }

        // Log to session file
        if (isReassignment)
        {
            await _sessionLogger.LogOperationAsync("REASSIGN_CARD", "Player", employee.Id,
                $"Card re-assigned. Old: {oldCardNo} (used {oldUsedVisits}/{oldMaxVisits}). New effectiveTimes: {cardDto.EffectiveTimes}",
                $"إعادة تعيين البطاقة. القديمة: {oldCardNo} (مستخدم {oldUsedVisits}/{oldMaxVisits}). مرات جديدة: {cardDto.EffectiveTimes}",
                _currentUser.Username);
        }
        else
        {
            await _sessionLogger.LogOperationAsync("ASSIGN_CARD", "AccessCard", card.Id,
                $"Assigned card {cardDto.CardNumber} to player {employee.FullNameEn}",
                $"تم تعيين بطاقة {cardDto.CardNumber} للاعب {employee.FullNameAr}",
                _currentUser.Username);
        }

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

        // Use card's ValidTo if it's in the future, otherwise use employee's EndDate, otherwise 10 years
        DateTime validEnd = card.ValidTo;
        if (validEnd < DateTime.Now)
        {
            var emp = await _employeeRepository.GetByIdWithCardsAsync(card.EmployeeId);
            validEnd = emp?.EndDate ?? DateTime.MinValue;
        }
        var permitTime = validEnd > DateTime.Now
            ? validEnd.ToString("yyyy-MM-dd HH:mm:ss")
            : DateTime.Now.AddYears(10).ToString("yyyy-MM-dd HH:mm:ss");

        var deviceInfo = BuildDeviceInfo(device);

        // Ping first, then execute SDK with lock (PauseMonitoring → SDK → ResumeMonitoring)
        var (success, error) = await _opHelper.PingThenExecuteAsync(device.IP, device.Name, () =>
        {
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
        });

        if (!success)
        {
            await _cardDeviceSyncRepository.UpsertAsync(cardId, deviceId, false, error);
            await _sessionLogger.LogErrorAsync("SYNC_CARD", "AccessCard", cardId,
                $"Failed to sync card {card.CardNumber} to device {device.Name}: {error}",
                _currentUser.Username);
            throw new InvalidOperationException(error);
        }

        // SDK succeeded — now update DB
        card.IsSyncedToDevice = true;
        await _cardRepository.UpdateAsync(card);
        await _cardDeviceSyncRepository.UpsertAsync(cardId, deviceId, true);

        await LogAuditAsync("SyncCard", "AccessCard", cardId,
            $"Synced card {card.CardNumber} to device {device.Name} ({device.IP})",
            $"تم مزامنة بطاقة {card.CardNumber} مع جهاز {device.Name} ({device.IP})");

        await _sessionLogger.LogOperationAsync("SYNC_CARD", "AccessCard", cardId,
            $"Successfully synced card {card.CardNumber} to device {device.Name}",
            $"تمت مزامنة بطاقة {card.CardNumber} مع جهاز {device.Name} بنجاح",
            _currentUser.Username);

        return true;
    }

    public async Task<(int activeMembers, int poolCodes)> GetDeviceSyncCountsAsync()
    {
        var cards = (await _cardRepository.GetAllActiveForSyncAsync()).ToList();
        var today = DateTime.Today;
        int members = cards.Count(c => c.ValidTo.Date >= today);
        int pool = await _qrPool.GetActivePoolCountAsync();
        return (members, pool);
    }

    public async Task<(int membersSynced, int membersFailed, int poolPushed, int poolFailed)> SyncAllDataToDeviceAsync(
        int deviceId, IProgress<DeviceSyncProgress>? progress = null, System.Threading.CancellationToken ct = default)
    {
        // Phase 1 — active-subscription members FIRST, so the gym is usable within seconds.
        var memberProgress = progress == null ? null : new Progress<(int current, int total, string cardNumber)>(
            p => progress.Report(new DeviceSyncProgress("Members", p.current, p.total)));
        var (mSynced, mFailed, _) = await SyncAllCardsToDeviceAsync(deviceId, memberProgress, activeSubscriptionsOnly: true);

        ct.ThrowIfCancellationRequested();

        // Phase 2 — the full QR pool (daily-pass + visitor); heavier, runs after members.
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return (mSynced, mFailed, 0, 0);
        var deviceInfo = BuildDeviceInfo(device);
        var poolProgress = progress == null ? null : new Progress<(int done, int total)>(
            p => progress.Report(new DeviceSyncProgress("QrPool", p.done, p.total)));
        var (pPushed, pFailed) = await _qrPool.ForcePushPoolToDeviceAsync(_sdk, deviceInfo, poolProgress, ct);

        return (mSynced, mFailed, pPushed, pFailed);
    }

    public async Task<(int synced, int failed, int total)> SyncAllCardsToDeviceAsync(
        int deviceId, IProgress<(int current, int total, string cardNumber)>? progress = null,
        bool activeSubscriptionsOnly = false)
    {
        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return (0, 0, 0);

        // Auto-create AccessCard records for players who have CardNo but no AccessCard
        // Check ALL cards (active + inactive) to avoid duplicate constraint violations
        var empCardInfos = (await _employeeRepository.GetCardInfoForSyncAsync()).ToList();
        var allExistingCards = (await _cardRepository.GetAllActiveForSyncAsync()).ToList();
        var existingCardNumbers = allExistingCards.Select(c => c.CardNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var emp in empCardInfos)
        {
            if (!string.IsNullOrWhiteSpace(emp.CardNo) && !existingCardNumbers.Contains(emp.CardNo))
            {
                try
                {
                    var newCard = new AccessCard
                    {
                        EmployeeId = emp.Id,
                        CardNumber = emp.CardNo,
                        IsActive = true,
                        ValidFrom = emp.StartDate,
                        ValidTo = emp.EndDate,
                        EffectiveTimes = emp.MaxVisits > 0 ? emp.MaxVisits : 65535,
                        DoorPermissions = "01010101",
                        TimePeriodIndex = 1,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _cardRepository.AddAsync(newCard);
                    existingCardNumbers.Add(emp.CardNo);
                }
                catch
                {
                    // Skip if duplicate or any DB error
                }
            }
        }

        // Step 2: Get all active cards for sync (no Employee navigation = no photos loaded)
        var allCards = (await _cardRepository.GetAllActiveForSyncAsync()).ToList();

        // When repopulating a new/replacement device we only push members whose subscription is still
        // valid — far fewer than the full roster, and the gate rejects expired cards anyway.
        if (activeSubscriptionsOnly)
        {
            var today = DateTime.Today;
            allCards = allCards.Where(c => c.ValidTo.Date >= today).ToList();
        }

        if (allCards.Count == 0) return (0, 0, 0);

        var deviceInfo = BuildDeviceInfo(device);

        // Upload in batches of 30 cards per SDK session
        // Each batch gets a full SDK cycle: Ping → Stop → Shutdown → ReInit → 30 cards → Cleanup
        int synced = 0, failed = 0;
        var cardsToSync = allCards.ToList();
        const int batchSize = 30;

        var batches = cardsToSync
            .Select((card, idx) => (card, idx))
            .GroupBy(x => x.idx / batchSize)
            .Select(g => g.Select(x => x.card).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            var currentBatch = batch;

            var batchResult = await _opHelper.ExecuteOnDevicesSequentialAsync(
                new[] { (deviceInfo, device.Name, device.IP, device.Id) },
                info =>
                {
                    foreach (var card in currentBatch)
                    {
                        progress?.Report((synced + failed + 1, cardsToSync.Count, card.CardNumber));
                        try
                        {
                            var permitTime = card.ValidTo > DateTime.Now
                                ? card.ValidTo.ToString("yyyy-MM-dd HH:mm:ss")
                                : DateTime.Now.AddYears(10).ToString("yyyy-MM-dd HH:mm:ss");

                            var doorPerm = card.DoorPermissions;
                            if (string.IsNullOrEmpty(doorPerm) || doorPerm == "01000000")
                                doorPerm = "01010000";

                            _sdk.AddAccessCard(
                                info,
                                card.CardNumber,
                                card.CardPassword,
                                card.OpenMode,
                                doorPerm,
                                permitTime,
                                card.EffectiveTimes > 0 ? card.EffectiveTimes : 65535,
                                card.TimePeriodIndex > 0 ? card.TimePeriodIndex : 1,
                                card.HolidayEnabled);

                            Thread.Sleep(200);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SyncAll] Card {card.CardNumber}: {ex.Message}");
                        }
                    }
                });

            if (batchResult.SuccessCount > 0)
            {
                foreach (var card in currentBatch)
                {
                    try
                    {
                        card.IsSyncedToDevice = true;
                        await _cardRepository.UpdateAsync(card);
                        synced++;
                    }
                    catch { failed++; }
                }
            }
            else
            {
                failed += currentBatch.Count;
            }

            await Task.Delay(500); // pause between batches
        }

        await LogAuditAsync("SyncAllCards", "Device", deviceId,
            $"Bulk synced {synced}/{allCards.Count} cards to device {device.Name} ({device.IP}). Failed: {failed}",
            $"تم مزامنة {synced}/{allCards.Count} بطاقة مع جهاز {device.Name} ({device.IP}). فشل: {failed}");

        return (synced, failed, allCards.Count);
    }

    public async Task<(int uploaded, int skipped, int failed, int total)> UploadAllCardsToDevicesAsync(
        IEnumerable<int> deviceIds, IProgress<(int current, int total, string cardNumber)>? progress = null)
    {
        // Step 1: Auto-create AccessCard records for migrated players
        var empCardInfos = (await _employeeRepository.GetCardInfoForSyncAsync()).ToList();
        var allExistingCards = (await _cardRepository.GetAllWithEmployeeAsync()).ToList();
        var existingCardNumbers = allExistingCards.Select(c => c.CardNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var emp in empCardInfos)
        {
            if (!string.IsNullOrWhiteSpace(emp.CardNo) && !existingCardNumbers.Contains(emp.CardNo))
            {
                try
                {
                    var newCard = new AccessCard
                    {
                        EmployeeId = emp.Id,
                        CardNumber = emp.CardNo,
                        IsActive = true,
                        ValidFrom = emp.StartDate,
                        ValidTo = emp.EndDate,
                        EffectiveTimes = emp.MaxVisits > 0 ? emp.MaxVisits : 65535,
                        DoorPermissions = "01010000",
                        TimePeriodIndex = 1,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _cardRepository.AddAsync(newCard);
                    existingCardNumbers.Add(emp.CardNo);
                }
                catch { }
            }
        }

        // Step 2: Get all active cards
        var allCards = (await _cardRepository.GetAllActiveForSyncAsync()).ToList();
        if (allCards.Count == 0) return (0, 0, 0, 0);

        var devices = await ResolveDevicesAsync(deviceIds);
        if (devices.Count == 0) return (0, 0, 0, allCards.Count);

        int totalUploaded = 0, totalSkipped = 0, totalFailed = 0;
        int progressIndex = 0;
        int totalWork = allCards.Count * devices.Count;

        // Step 3: Upload using the SAME SDK pattern as individual assign
        // (Ping → StopMonitor → Shutdown → ReInit → SDK call → Cleanup)
        foreach (var device in devices)
        {
            var deviceInfo = BuildDeviceInfo(device);

            // Get cards already synced to this device
            var syncedRecords = await _cardDeviceSyncRepository.GetByDeviceIdAsync(device.Id);
            var alreadySynced = syncedRecords
                .Where(s => s.IsSynced)
                .Select(s => s.AccessCardId)
                .ToHashSet();

            // Build list of cards to upload (skip already synced)
            var cardsToUpload = new List<AccessCard>();
            foreach (var card in allCards)
            {
                progressIndex++;
                if (alreadySynced.Contains(card.Id))
                {
                    totalSkipped++;
                    continue;
                }
                cardsToUpload.Add(card);
            }

            if (cardsToUpload.Count == 0) continue;

            // Upload in batches of 30 cards per SDK session
            // Each batch: Ping → StopMonitor → Shutdown → ReInit → 30 cards → Cleanup
            const int batchSize = 30;
            var batches = cardsToUpload
                .Select((card, idx) => (card, idx))
                .GroupBy(x => x.idx / batchSize)
                .Select(g => g.Select(x => x.card).ToList())
                .ToList();

            int batchNum = 0;
            foreach (var batch in batches)
            {
                batchNum++;
                var currentBatch = batch; // capture for lambda

                var batchResult = await _opHelper.ExecuteOnDevicesSequentialAsync(
                    new[] { (deviceInfo, device.Name, device.IP, device.Id) },
                    info =>
                    {
                        foreach (var card in currentBatch)
                        {
                            progress?.Report((totalUploaded + totalSkipped + totalFailed + 1, totalWork, card.CardNumber));

                            var permitTime = card.ValidTo > DateTime.Now
                                ? card.ValidTo.ToString("yyyy-MM-dd HH:mm:ss")
                                : DateTime.Now.AddYears(10).ToString("yyyy-MM-dd HH:mm:ss");

                            var doorPerm = card.DoorPermissions;
                            if (string.IsNullOrEmpty(doorPerm) || doorPerm == "01000000")
                                doorPerm = "01010000"; // default Door 1+2

                            _sdk.AddAccessCard(
                                info,
                                card.CardNumber,
                                card.CardPassword,
                                card.OpenMode,
                                doorPerm,
                                permitTime,
                                card.EffectiveTimes > 0 ? card.EffectiveTimes : 65535,
                                card.TimePeriodIndex > 0 ? card.TimePeriodIndex : 1,
                                card.HolidayEnabled);

                            Thread.Sleep(200);
                        }
                    });

                // Update DB for this batch
                if (batchResult.SuccessCount > 0)
                {
                    foreach (var card in currentBatch)
                    {
                        try
                        {
                            card.IsSyncedToDevice = true;
                            await _cardRepository.UpdateAsync(card);
                            await _cardDeviceSyncRepository.UpsertAsync(card.Id, device.Id, true);
                            totalUploaded++;
                        }
                        catch { totalFailed++; }
                    }
                }
                else
                {
                    foreach (var card in currentBatch)
                    {
                        try { await _cardDeviceSyncRepository.UpsertAsync(card.Id, device.Id, false, "Batch failed"); } catch { }
                        totalFailed++;
                    }
                }

                // Brief pause between batches to let device settle
                await Task.Delay(500);
            }
        }

        await LogAuditAsync("UploadAllCards", "System", null,
            $"Upload all cards to {devices.Count} device(s): Uploaded={totalUploaded}, Skipped={totalSkipped}, Failed={totalFailed}",
            $"رفع جميع البطاقات إلى {devices.Count} جهاز: تم الرفع={totalUploaded}، تم التخطي={totalSkipped}، فشل={totalFailed}");

        return (totalUploaded, totalSkipped, totalFailed, allCards.Count * devices.Count);
    }

    public async Task<(int synced, int failed, int total, List<string> errors)> SyncCardToDevicesAsync(int cardId, IEnumerable<int>? deviceIds = null)
    {
        var card = await _cardRepository.GetByIdAsync(cardId);
        if (card == null)
            throw new InvalidOperationException($"Card with ID {cardId} not found.");

        var devices = await ResolveDevicesAsync(deviceIds);
        if (devices.Count == 0)
            return (0, 0, 0, new List<string> { "No devices found." });

        var permitTime = card.ValidTo > DateTime.Now
            ? card.ValidTo.ToString("yyyy-MM-dd HH:mm:ss")
            : DateTime.Now.AddYears(10).ToString("yyyy-MM-dd HH:mm:ss");

        // Build device tuples for sequential execution with ping-first pattern
        var deviceTuples = devices.Select(d => (BuildDeviceInfo(d), d.Name, d.IP, d.Id));

        var result = await _opHelper.ExecuteOnDevicesSequentialAsync(deviceTuples, deviceInfo =>
        {
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
        });

        // Update DB sync status only for devices that succeeded
        foreach (var succeeded in result.Succeeded)
            await _cardDeviceSyncRepository.UpsertAsync(cardId, succeeded.DeviceId, true);

        foreach (var failed in result.Failed)
            await _cardDeviceSyncRepository.UpsertAsync(cardId, failed.DeviceId, false, failed.Error);

        // Only mark card as synced if at least one device succeeded
        card.IsSyncedToDevice = result.SuccessCount > 0;
        await _cardRepository.UpdateAsync(card);

        await LogAuditAsync("SyncCardAllDevices", "AccessCard", cardId,
            $"Synced card {card.CardNumber} to {result.SuccessCount}/{devices.Count} devices. Failed: {result.FailedCount}",
            $"تم مزامنة بطاقة {card.CardNumber} مع {result.SuccessCount}/{devices.Count} جهاز. فشل: {result.FailedCount}");

        var errors = result.Failed.Select(f => $"{f.Name} ({f.IP}): {f.Error}").ToList();
        return (result.SuccessCount, result.FailedCount, devices.Count, errors);
    }

    public async Task<(int synced, int failed, int total)> SyncAllCardsToDevicesAsync(
        IEnumerable<int>? deviceIds = null, IProgress<(int current, int total, string cardNumber)>? progress = null)
    {
        var devices = await ResolveDevicesAsync(deviceIds);
        if (devices.Count == 0) return (0, 0, 0);

        var allCards = (await _cardRepository.GetAllWithEmployeeAsync())
            .Where(c => c.IsActive && c.Employee != null)
            .ToList();

        if (allCards.Count == 0) return (0, 0, 0);

        int synced = 0, failed = 0;
        int totalOps = allCards.Count * devices.Count;
        int current = 0;

        // Sequential: for each card, ping-then-execute on each device
        foreach (var card in allCards)
        {
            var permitTime = card.ValidTo > DateTime.Now
                ? card.ValidTo.ToString("yyyy-MM-dd HH:mm:ss")
                : DateTime.Now.AddYears(10).ToString("yyyy-MM-dd HH:mm:ss");

            var deviceTuples = devices.Select(d => (BuildDeviceInfo(d), d.Name, d.IP, d.Id));

            var cardResult = await _opHelper.ExecuteOnDevicesSequentialAsync(deviceTuples, deviceInfo =>
            {
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
            });

            // Update sync tracking per device
            foreach (var s in cardResult.Succeeded)
                await _cardDeviceSyncRepository.UpsertAsync(card.Id, s.DeviceId, true);
            foreach (var f in cardResult.Failed)
                await _cardDeviceSyncRepository.UpsertAsync(card.Id, f.DeviceId, false, f.Error);

            synced += cardResult.SuccessCount;
            failed += cardResult.FailedCount;
            current += devices.Count;
            progress?.Report((current, totalOps, card.CardNumber));

            // Only mark card synced if at least one device succeeded
            card.IsSyncedToDevice = cardResult.SuccessCount > 0;
            await _cardRepository.UpdateAsync(card);
        }

        await LogAuditAsync("SyncAllCardsAllDevices", "System", null,
            $"Bulk synced {synced}/{totalOps} card-device pairs ({allCards.Count} cards × {devices.Count} devices). Failed: {failed}",
            $"تم مزامنة {synced}/{totalOps} بطاقة-جهاز ({allCards.Count} بطاقة × {devices.Count} جهاز). فشل: {failed}");

        return (synced, failed, totalOps);
    }

    public async Task<(int synced, int failed, int total, List<string> errors)> RemoveCardFromDevicesAsync(int cardId, IEnumerable<int>? deviceIds = null)
    {
        var card = await _cardRepository.GetByIdAsync(cardId);
        if (card == null)
            throw new InvalidOperationException($"Card with ID {cardId} not found.");

        var devices = await ResolveDevicesAsync(deviceIds);
        if (devices.Count == 0)
            return (0, 0, 0, new List<string>());

        var expireDate = DateTime.Now.AddMinutes(1).ToString("yyyy-MM-dd HH:mm:ss");

        // Build device tuples and execute sequentially with ping-first pattern
        var deviceTuples = devices.Select(d => (BuildDeviceInfo(d), d.Name, d.IP, d.Id));

        var result = await _opHelper.ExecuteOnDevicesSequentialAsync(deviceTuples, deviceInfo =>
        {
            _sdk.AddAccessCard(
                deviceInfo,
                card.CardNumber,
                card.CardPassword,
                card.OpenMode,
                card.DoorPermissions,
                expireDate,
                1,
                card.TimePeriodIndex,
                card.HolidayEnabled);
        });

        // Only clean up sync records for devices that succeeded
        foreach (var s in result.Succeeded)
            await _cardDeviceSyncRepository.UpsertAsync(cardId, s.DeviceId, false, "Card removed from device");

        // Mark card as not synced only if all devices succeeded
        if (result.AllSucceeded)
        {
            await _cardDeviceSyncRepository.DeleteByCardIdAsync(cardId);
            card.IsSyncedToDevice = false;
            await _cardRepository.UpdateAsync(card);
        }
        else if (result.SuccessCount > 0)
        {
            // Partial success — card still synced on failed devices
            card.IsSyncedToDevice = result.FailedCount > 0;
            await _cardRepository.UpdateAsync(card);
        }

        var errors = result.Failed.Select(f => $"{f.Name} ({f.IP}): {f.Error}").ToList();
        return (result.SuccessCount, result.FailedCount, devices.Count, errors);
    }

    public async Task<(int ok, int fail, int total, List<string> errors)> PushTempCardToDevicesAsync(
        string cardNumber, DateTime validTo, string doorPermissions, IEnumerable<int>? deviceIds = null,
        int maxUses = 65535)
    {
        cardNumber = (cardNumber ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cardNumber))
            return (0, 0, 0, new List<string> { "Empty card number." });

        var devices = await ResolveDevicesAsync(deviceIds);
        if (devices.Count == 0)
            return (0, 0, 0, new List<string> { "No devices found." });

        // Date-enforced expiry: the gate rejects the card after permitTime regardless of uses.
        var permitTime = (validTo > DateTime.Now ? validTo : DateTime.Now.AddHours(1))
            .ToString("yyyy-MM-dd HH:mm:ss");
        var doors = string.IsNullOrWhiteSpace(doorPermissions) ? "01010000" : doorPermissions;
        var deviceTuples = devices.Select(d => (BuildDeviceInfo(d), d.Name, d.IP, d.Id));

        // effectiveTimes = use count the gate enforces. Daily Pass passes 2 (one in + one out)
        // so a found/shared ticket can't be reused all day; default stays high (date-governed).
        var effectiveTimes = maxUses <= 0 ? 65535 : maxUses;
        var result = await _opHelper.ExecuteOnDevicesSequentialAsync(deviceTuples, deviceInfo =>
        {
            _sdk.AddAccessCard(deviceInfo, cardNumber, "", 0, doors, permitTime, effectiveTimes, 0, false);
        });

        await LogAuditAsync("DailyPassPush", "AccessCard", 0,
            $"Daily-pass card {cardNumber} pushed to {result.SuccessCount}/{devices.Count} devices, valid until {permitTime}",
            $"بطاقة دخول يومي {cardNumber} أُرسلت إلى {result.SuccessCount}/{devices.Count} جهاز، صالحة حتى {permitTime}");

        var errors = result.Failed.Select(f => $"{f.Name} ({f.IP}): {f.Error}").ToList();
        return (result.SuccessCount, result.FailedCount, devices.Count, errors);
    }

    public async Task<(int ok, int fail, int total, List<string> errors)> ExpireTempCardOnDevicesAsync(
        string cardNumber, IEnumerable<int>? deviceIds = null)
    {
        cardNumber = (cardNumber ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cardNumber))
            return (0, 0, 0, new List<string>());

        var devices = await ResolveDevicesAsync(deviceIds);
        if (devices.Count == 0)
            return (0, 0, 0, new List<string>());

        // Re-add with an immediate expiry → the controller rejects it from now on.
        var expireDate = DateTime.Now.AddMinutes(1).ToString("yyyy-MM-dd HH:mm:ss");
        var deviceTuples = devices.Select(d => (BuildDeviceInfo(d), d.Name, d.IP, d.Id));

        var result = await _opHelper.ExecuteOnDevicesSequentialAsync(deviceTuples, deviceInfo =>
        {
            _sdk.AddAccessCard(deviceInfo, cardNumber, "", 0, "01010000", expireDate, 1, 0, false);
        });

        var errors = result.Failed.Select(f => $"{f.Name} ({f.IP}): {f.Error}").ToList();
        return (result.SuccessCount, result.FailedCount, devices.Count, errors);
    }

    public async Task IncrementVisitAsync(string cardNumber)
    {
        var card = await _cardRepository.GetByCardNumberAsync(cardNumber);
        if (card?.Employee == null) return;

        var employee = card.Employee;
        if (employee.MaxVisits <= 0) return;

        employee.UsedVisits++;
        await _employeeRepository.UpdateAsync(employee);

        if (employee.UsedVisits >= employee.MaxVisits)
        {
            // Hardware handles expiry via effectiveTimes counter — no SDK disable needed
            await LogAuditAsync("VisitLimitReached", "Employee", employee.Id,
                $"Player {employee.FullNameEn} reached visit limit ({employee.UsedVisits}/{employee.MaxVisits}).",
                $"اللاعب {employee.FullNameAr} وصل حد الزيارات ({employee.UsedVisits}/{employee.MaxVisits}).");
        }
    }

    public async Task<(bool isValid, string reason)> ValidateCardOnSwipeAsync(string cardNumber, bool isEntry = true)
    {
        var card = await _cardRepository.GetByCardNumberAsync(cardNumber);
        if (card == null)
            return (false, "Card not registered");

        if (!card.IsActive)
            return (false, "Card is deactivated");

        var employee = card.Employee;
        if (employee == null)
            return (false, "No player linked to card");

        if (employee.IsFrozen)
        {
            return (false, $"Player is frozen since {employee.FreezeStartDate:yyyy-MM-dd}");
        }

        // Check date-based expiry (use .Date to allow access for full last day)
        if (employee.EndDate.Date < DateTime.Today)
        {
            return (false, $"Subscription expired ({employee.EndDate:yyyy-MM-dd})");
        }

        // Check visit-count expiry — validate FIRST, then increment only if allowed
        if (employee.MaxVisits > 0)
        {
            if (employee.UsedVisits >= employee.MaxVisits)
            {
                return (false, $"Visit limit reached ({employee.UsedVisits}/{employee.MaxVisits})");
            }

            employee.UsedVisits++;
            await _employeeRepository.UpdateAsync(employee);

            return (true, $"Visit {employee.UsedVisits}/{employee.MaxVisits}");
        }

        return (true, "Valid");
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

    /// <summary>
    /// Resolves target devices: null → ALL devices, otherwise only the specified IDs.
    /// </summary>
    private async Task<List<Device>> ResolveDevicesAsync(IEnumerable<int>? deviceIds)
    {
        var all = (await _deviceRepository.GetAllAsync()).ToList();
        if (deviceIds == null) return all;
        var ids = deviceIds.ToHashSet();
        return all.Where(d => ids.Contains(d.Id)).ToList();
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

    private async Task DisableCardsOnHardware(Employee employee, IEnumerable<int>? deviceIds = null)
    {
        var cards = employee.AccessCards?.Where(c => c.IsActive).ToList();
        if (cards == null || cards.Count == 0) return;

        var devices = await ResolveDevicesAsync(deviceIds);
        if (devices.Count == 0) return;

        foreach (var card in cards)
        {
            // Single attempt only — no retries. Hardware effectiveTimes countdown handles expiry.
            var pastDate = DateTime.Now.AddMinutes(-1).ToString("yyyy-MM-dd HH:mm:ss");

            var deviceTuples = devices.Select(d => (BuildDeviceInfo(d), d.Name, d.IP, d.Id));

            var result = await _opHelper.ExecuteOnDevicesSequentialAsync(deviceTuples, deviceInfo =>
            {
                _sdk.AddAccessCard(
                    deviceInfo,
                    card.CardNumber, card.CardPassword,
                    card.OpenMode, card.DoorPermissions,
                    pastDate, 1,
                    card.TimePeriodIndex, card.HolidayEnabled);
            });

            foreach (var s in result.Succeeded)
                LogDisableResult(card.CardNumber, devices.FirstOrDefault(d => d.Id == s.DeviceId)?.SerialNumber ?? s.DeviceId.ToString(), pastDate, "SUCCESS");
            foreach (var f in result.Failed)
                LogDisableResult(card.CardNumber, devices.FirstOrDefault(d => d.Id == f.DeviceId)?.SerialNumber ?? f.DeviceId.ToString(), pastDate, $"FAILED: {f.Error}");
        }
    }

    private static readonly string DisableLogPath = Path.Combine(AppContext.BaseDirectory, "card_disable_log.txt");

    private static void LogDisableResult(string cardNo, string deviceSN, string date, string result)
    {
        try
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Card={cardNo} Device={deviceSN} Date={date} → {result}\n";
            RollingLogFile.Append(DisableLogPath, msg);
        }
        catch { }
    }

    private async Task ReEnableCardsOnHardware(Employee employee, IEnumerable<int>? deviceIds = null, DateTime? newEndDate = null, int? effectiveTimes = null)
    {
        var syncedCards = employee.AccessCards?.Where(c => c.IsActive && c.IsSyncedToDevice).ToList();
        if (syncedCards == null || syncedCards.Count == 0) return;

        var devices = await ResolveDevicesAsync(deviceIds);
        if (devices.Count == 0) return;

        // Use the provided newEndDate if given (for unfreeze with extended date), otherwise use employee.EndDate
        var permitEnd = newEndDate ?? employee.EndDate;
        var newPermitTime = permitEnd.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (var card in syncedCards)
        {
            // Update card's ValidTo in DB to match the new permit time
            card.ValidTo = permitEnd;
            // Update card's EffectiveTimes if a custom value was provided (e.g. remaining visits on unfreeze)
            var hwEffectiveTimes = effectiveTimes ?? card.EffectiveTimes;
            if (effectiveTimes.HasValue)
            {
                card.EffectiveTimes = hwEffectiveTimes;
            }
            await _cardRepository.UpdateAsync(card);

            // Ping each device first, then execute SDK sequentially (uses _opHelper lock)
            var deviceTuples = devices.Select(d => (BuildDeviceInfo(d), d.Name, d.IP, d.Id));

            var result = await _opHelper.ExecuteOnDevicesSequentialAsync(deviceTuples, deviceInfo =>
            {
                _sdk.AddAccessCard(
                    deviceInfo,
                    card.CardNumber,
                    card.CardPassword,
                    card.OpenMode,
                    card.DoorPermissions,
                    newPermitTime,
                    hwEffectiveTimes,
                    card.TimePeriodIndex,
                    card.HolidayEnabled);
            });

            foreach (var s in result.Succeeded)
                await _cardDeviceSyncRepository.UpsertAsync(card.Id, s.DeviceId, true);

            foreach (var f in result.Failed)
                await _cardDeviceSyncRepository.UpsertAsync(card.Id, f.DeviceId, false, f.Error);
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
        Debt = e.Debt,
        MaxVisits = e.MaxVisits,
        UsedVisits = e.UsedVisits,
        CardCount = e.AccessCards?.Count ?? 0,
        CreatedAt = e.CreatedAt,
        SyncStatus = ComputeSyncStatus(e),
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

    private static int ComputeSyncStatus(Employee e)
    {
        var cards = e.AccessCards;
        if ((cards == null || cards.Count == 0) && string.IsNullOrWhiteSpace(e.CardNo))
            return 0; // NoCard
        if (cards == null || cards.Count == 0)
            return 1; // Has CardNo but no AccessCard records = NotSynced
        var activeCards = cards.Where(c => c.IsActive).ToList();
        if (activeCards.Count == 0)
            return 1; // No active cards = NotSynced
        var syncedCount = activeCards.Count(c => c.IsSyncedToDevice);
        if (syncedCount == 0) return 1; // NotSynced
        if (syncedCount < activeCards.Count) return 2; // PartiallySynced
        return 3; // FullySynced
    }
}
