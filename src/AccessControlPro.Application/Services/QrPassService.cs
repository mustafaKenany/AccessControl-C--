using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class QrPassService : IQrPassService
{
    private readonly IQrPassRepository _qrPassRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IQrPoolService _qrPoolService;
    private readonly CurrentUserService _currentUser;

    public QrPassService(
        IQrPassRepository qrPassRepository,
        ITransactionRepository transactionRepository,
        IQrPoolService qrPoolService,
        CurrentUserService currentUser)
    {
        _qrPassRepository = qrPassRepository;
        _transactionRepository = transactionRepository;
        _qrPoolService = qrPoolService;
        _currentUser = currentUser;
    }

    public async Task<QrPassDto> CreatePassAsync(string playerName, string phone, decimal fee, int maxUses = 5, int validDays = 1,
        int? deviceId = null, int doorNumber = 1, string deviceName = "")
    {
        var now = DateTime.Now;

        // Pull a code from the QR Pool (these are pre-uploaded to the access control device,
        // so the QR will actually work at the door reader). Earlier versions generated a
        // random string like "QR05040944441143" which the device never knew about — door denied.
        var poolEntry = await _qrPoolService.AssignCodeAsync(
            playerName.Trim(),
            phone.Trim(),
            $"Daily Pass — {validDays}d, {maxUses} uses, fee {fee:N0}");

        if (poolEntry == null)
            throw new InvalidOperationException(
                "No QR codes available in the pool. The pool tops itself up automatically — wait a few minutes and try again, or restart the app to trigger generation now.");

        var passCode = poolEntry.Code; // numeric pool code (e.g. "50001050") that the device recognises

        var qrPass = new QrPass
        {
            PassCode = passCode,
            PlayerName = playerName.Trim(),
            Phone = phone.Trim(),
            ValidFrom = now,
            ValidTo = now.Date.AddDays(validDays).AddHours(23).AddMinutes(59).AddSeconds(59),
            MaxUses = maxUses,
            UsedCount = 0,
            Fee = fee,
            IsActive = true,
            CreatedBy = _currentUser.Username ?? "system",
            CreatedAt = DateTime.UtcNow,
            DeviceId = deviceId,
            DoorNumber = doorNumber,
            DeviceName = deviceName
        };

        await _qrPassRepository.AddAsync(qrPass);

        // Record income transaction for the daily pass fee
        if (fee > 0)
        {
            var transaction = new Transaction
            {
                Type = Domain.Enums.TransactionType.Income,
                Category = "Daily Pass",
                Amount = fee,
                Description = $"QR Daily Pass - {playerName}",
                PaymentMethod = Domain.Enums.PaymentMethod.Cash,
                CreatedBy = _currentUser.Username ?? "system",
                CreatedAt = DateTime.UtcNow
            };
            await _transactionRepository.AddAsync(transaction);
        }

        QrPoolService.Log($"CreatePass OK: code={passCode} player={playerName} fee={fee} validUntil={qrPass.ValidTo:yyyy-MM-dd HH:mm} maxUses={maxUses}");
        return MapToDto(qrPass);
    }

    public async Task<(bool IsValid, string Message, QrPassDto? Pass)> ValidateAndUseAsync(string passCode)
    {
        var pass = await _qrPassRepository.GetByPassCodeAsync(passCode);

        if (pass == null)
        {
            QrPoolService.Log($"ValidateAndUse: code={passCode} not found", "warn");
            return (false, "QR code not found", null);
        }

        if (!pass.IsActive)
        {
            QrPoolService.Log($"ValidateAndUse: code={passCode} player={pass.PlayerName} deactivated", "warn");
            return (false, "QR pass has been deactivated", MapToDto(pass));
        }

        var now = DateTime.Now;
        if (now < pass.ValidFrom)
        {
            QrPoolService.Log($"ValidateAndUse: code={passCode} player={pass.PlayerName} not-yet-valid (validFrom={pass.ValidFrom:O})", "warn");
            return (false, "QR pass is not yet valid", MapToDto(pass));
        }

        if (now > pass.ValidTo)
        {
            pass.IsActive = false;
            await _qrPassRepository.UpdateAsync(pass);
            QrPoolService.Log($"ValidateAndUse: code={passCode} player={pass.PlayerName} expired (validTo={pass.ValidTo:O})", "warn");
            return (false, "QR pass has expired", MapToDto(pass));
        }

        if (pass.UsedCount >= pass.MaxUses)
        {
            pass.IsActive = false;
            await _qrPassRepository.UpdateAsync(pass);
            QrPoolService.Log($"ValidateAndUse: code={passCode} player={pass.PlayerName} max-uses-reached ({pass.UsedCount}/{pass.MaxUses})", "warn");
            return (false, "QR pass has reached maximum uses", MapToDto(pass));
        }

        // Valid — increment use count
        pass.UsedCount++;
        if (pass.UsedCount >= pass.MaxUses)
            pass.IsActive = false;

        await _qrPassRepository.UpdateAsync(pass);

        var remaining = pass.MaxUses - pass.UsedCount;
        QrPoolService.Log($"ValidateAndUse OK: code={passCode} player={pass.PlayerName} usesNow={pass.UsedCount}/{pass.MaxUses}");
        return (true, $"Access granted. {remaining} uses remaining.", MapToDto(pass));
    }

    public async Task<(IEnumerable<QrPassDto> Items, int TotalCount)> GetPassesPagedAsync(
        int page, int pageSize, string? search = null, bool? activeOnly = null)
    {
        var (items, total) = await _qrPassRepository.GetPagedAsync(page, pageSize, search, activeOnly);
        return (items.Select(MapToDto), total);
    }

    public async Task<QrPassDto?> GetPassByIdAsync(int id)
    {
        var pass = await _qrPassRepository.GetByIdAsync(id);
        return pass != null ? MapToDto(pass) : null;
    }

    public async Task DeactivatePassAsync(int id)
    {
        var pass = await _qrPassRepository.GetByIdAsync(id);
        if (pass == null) throw new InvalidOperationException("QR pass not found");
        pass.IsActive = false;
        await _qrPassRepository.UpdateAsync(pass);
        QrPoolService.Log($"DeactivatePass: id={id} code={pass.PassCode} player={pass.PlayerName}");
    }

    public async Task<int> ExpireOldPassesAsync()
    {
        var expired = await _qrPassRepository.GetExpiredActivePassesAsync();
        var count = 0;
        foreach (var pass in expired)
        {
            pass.IsActive = false;
            await _qrPassRepository.UpdateAsync(pass);
            count++;
        }
        if (count > 0) QrPoolService.Log($"ExpireOldPasses: expired={count}");
        return count;
    }

    public async Task<int> GetActiveTodayCountAsync()
    {
        return await _qrPassRepository.GetActiveTodayCountAsync();
    }

    // GeneratePassCode() removed — daily passes now pull a code from the QR pool via
    // QrPoolService.AssignCodeAsync so the QR actually works at the access control device.
    // The old random "QR{timestamp}{suffix}" format generated codes the device never knew
    // about, so the door always denied them.

    private static QrPassDto MapToDto(QrPass pass) => new()
    {
        Id = pass.Id,
        PassCode = pass.PassCode,
        PlayerName = pass.PlayerName,
        Phone = pass.Phone,
        ValidFrom = pass.ValidFrom,
        ValidTo = pass.ValidTo,
        MaxUses = pass.MaxUses,
        UsedCount = pass.UsedCount,
        Fee = pass.Fee,
        IsActive = pass.IsActive,
        CreatedBy = pass.CreatedBy,
        CreatedAt = pass.CreatedAt,
        DeviceId = pass.DeviceId,
        DoorNumber = pass.DoorNumber,
        DeviceName = pass.DeviceName
    };
}
