using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
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
    private readonly CurrentUserService _currentUser;

    public EmployeeService(
        IEmployeeRepository employeeRepository,
        IAccessCardRepository cardRepository,
        IDeviceRepository deviceRepository,
        IAccessControlSdk sdk,
        IAuditLogRepository auditLogRepository,
        IDeletedEmployeeRepository deletedEmployeeRepository,
        IFreezeHistoryRepository freezeHistoryRepository,
        CurrentUserService currentUser)
    {
        _employeeRepository = employeeRepository;
        _cardRepository = cardRepository;
        _deviceRepository = deviceRepository;
        _sdk = sdk;
        _auditLogRepository = auditLogRepository;
        _deletedEmployeeRepository = deletedEmployeeRepository;
        _freezeHistoryRepository = freezeHistoryRepository;
        _currentUser = currentUser;
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
        await LogAuditAsync("Create", "Player", employee.Id,
            $"Added player: {dto.FullNameEn} ({dto.CardNo})",
            $"تم إضافة لاعب: {dto.FullNameAr} ({dto.CardNo})");
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
        var reasonEn = string.IsNullOrWhiteSpace(editReason) ? "" : $" Reason: {editReason}";
        var reasonAr = string.IsNullOrWhiteSpace(editReason) ? "" : $" السبب: {editReason}";
        await LogAuditAsync("Update", "Player", employee.Id,
            $"Updated player: {dto.FullNameEn} ({dto.CardNo}).{reasonEn}",
            $"تم تعديل لاعب: {dto.FullNameAr} ({dto.CardNo}).{reasonAr}");
        return true;
    }

    public async Task<bool> DeleteEmployeeAsync(int id)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        if (employee == null) return false;

        await LogAuditAsync("Delete", "Player", id,
            $"Deleted player: {employee.FullNameEn} ({employee.CardNo})",
            $"تم حذف لاعب: {employee.FullNameAr} ({employee.CardNo})");
        await _employeeRepository.DeleteAsync(id);
        return true;
    }

    public async Task<bool> SoftDeleteEmployeeAsync(int id, string reason)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        if (employee == null) return false;

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

        // Delete from active table
        await _employeeRepository.DeleteAsync(id);
        return true;
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
        return true;
    }

    public async Task<bool> UnfreezePlayerAsync(int id)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        if (employee == null || !employee.IsFrozen || employee.FreezeStartDate == null) return false;

        var freezeDays = (int)(DateTime.UtcNow - employee.FreezeStartDate.Value).TotalDays;
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
        return true;
    }

    public async Task<bool> RenewSubscriptionAsync(int id, string subscriptionType, int months, decimal fee, decimal amountPaid)
    {
        var employee = await _employeeRepository.GetByIdWithCardsAsync(id);
        if (employee == null) return false;

        var oldType = employee.SubscriptionType;
        var oldEndDate = employee.EndDate;

        employee.SubscriptionType = subscriptionType;
        employee.StartDate = DateTime.Today;
        employee.EndDate = DateTime.Today.AddMonths(months);
        employee.SubscriptionFee = fee;
        employee.AmountPaid = amountPaid;
        employee.IsFrozen = false;
        employee.FreezeStartDate = null;
        await _employeeRepository.UpdateAsync(employee);

        await LogAuditAsync("Renew", "Player", id,
            $"Renewed subscription for {employee.FullNameEn} ({employee.CardNo}). " +
            $"Type: {oldType} → {subscriptionType}. " +
            $"Period: {months} months. Fee: {fee}. Paid: {amountPaid}. " +
            $"Old EndDate: {oldEndDate:yyyy-MM-dd} → New EndDate: {employee.EndDate:yyyy-MM-dd}",
            $"تم تجديد اشتراك {employee.FullNameAr} ({employee.CardNo}). " +
            $"النوع: {oldType} → {subscriptionType}. " +
            $"المدة: {months} شهر. الرسوم: {fee}. المدفوع: {amountPaid}. " +
            $"تاريخ الانتهاء القديم: {oldEndDate:yyyy-MM-dd} → الجديد: {employee.EndDate:yyyy-MM-dd}");
        return true;
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
        await _cardRepository.AddAsync(card);
        await LogAuditAsync("Create", "AccessCard", card.Id,
            $"Assigned card {cardDto.CardNumber} to {employee.FullNameEn}",
            $"تم تعيين بطاقة {cardDto.CardNumber} إلى {employee.FullNameAr}");
        return true;
    }

    public async Task<bool> RemoveCardAsync(int cardId)
    {
        var card = await _cardRepository.GetByIdAsync(cardId);
        if (card == null) return false;

        await LogAuditAsync("Delete", "AccessCard", cardId,
            $"Removed card {card.CardNumber} from player {card.EmployeeId}",
            $"تم إزالة بطاقة {card.CardNumber} من اللاعب {card.EmployeeId}");
        await _cardRepository.DeleteAsync(cardId);
        return true;
    }

    public async Task<bool> SyncCardToDeviceAsync(int cardId, int deviceId)
    {
        var card = await _cardRepository.GetByIdAsync(cardId);
        if (card == null) return false;

        var device = await _deviceRepository.GetByIdAsync(deviceId);
        if (device == null) return false;

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
        await LogAuditAsync("SyncCard", "AccessCard", cardId,
            $"Synced card {card.CardNumber} to device {device.Name} ({device.IP})",
            $"تم مزامنة بطاقة {card.CardNumber} مع جهاز {device.Name} ({device.IP})");
        return true;
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
        var devices = await _deviceRepository.GetAllAsync();

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
                }
                catch { /* device offline or unreachable — skip */ }
            }
        }
    }

    private async Task ReEnableCardsOnHardware(Employee employee)
    {
        var syncedCards = employee.AccessCards?.Where(c => c.IsActive && c.IsSyncedToDevice).ToList();
        if (syncedCards == null || syncedCards.Count == 0) return;

        _sdk.Initialize();
        var devices = await _deviceRepository.GetAllAsync();
        var newPermitTime = employee.EndDate.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (var card in syncedCards)
        {
            // Update card validity in database to match extended EndDate
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
                }
                catch { /* device offline or unreachable — skip */ }
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
        CardCount = e.AccessCards.Count,
        CreatedAt = e.CreatedAt,
        Cards = e.AccessCards.Select(c => new AccessCardDto
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
