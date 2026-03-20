using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;

namespace AccessControlPro.Application.Services;

public class QrPassService : IQrPassService
{
    private readonly IQrPassRepository _qrPassRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly CurrentUserService _currentUser;

    public QrPassService(
        IQrPassRepository qrPassRepository,
        ITransactionRepository transactionRepository,
        CurrentUserService currentUser)
    {
        _qrPassRepository = qrPassRepository;
        _transactionRepository = transactionRepository;
        _currentUser = currentUser;
    }

    public async Task<QrPassDto> CreatePassAsync(string playerName, string phone, decimal fee, int maxUses = 5, int validDays = 1,
        int? deviceId = null, int doorNumber = 1, string deviceName = "")
    {
        var now = DateTime.Now;
        var passCode = GeneratePassCode();

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

        return MapToDto(qrPass);
    }

    public async Task<(bool IsValid, string Message, QrPassDto? Pass)> ValidateAndUseAsync(string passCode)
    {
        var pass = await _qrPassRepository.GetByPassCodeAsync(passCode);

        if (pass == null)
            return (false, "QR code not found", null);

        if (!pass.IsActive)
            return (false, "QR pass has been deactivated", MapToDto(pass));

        var now = DateTime.Now;
        if (now < pass.ValidFrom)
            return (false, "QR pass is not yet valid", MapToDto(pass));

        if (now > pass.ValidTo)
        {
            pass.IsActive = false;
            await _qrPassRepository.UpdateAsync(pass);
            return (false, "QR pass has expired", MapToDto(pass));
        }

        if (pass.UsedCount >= pass.MaxUses)
        {
            pass.IsActive = false;
            await _qrPassRepository.UpdateAsync(pass);
            return (false, "QR pass has reached maximum uses", MapToDto(pass));
        }

        // Valid — increment use count
        pass.UsedCount++;
        if (pass.UsedCount >= pass.MaxUses)
            pass.IsActive = false;

        await _qrPassRepository.UpdateAsync(pass);

        var remaining = pass.MaxUses - pass.UsedCount;
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
        return count;
    }

    public async Task<int> GetActiveTodayCountAsync()
    {
        return await _qrPassRepository.GetActiveTodayCountAsync();
    }

    private static string GeneratePassCode()
    {
        // Generate a unique code using cryptographic RNG (not predictable)
        var timestamp = DateTime.UtcNow.ToString("MMddHHmmss");
        var suffixBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(4);
        var suffix = (Math.Abs(BitConverter.ToInt32(suffixBytes, 0)) % 9000 + 1000).ToString();
        return $"QR{timestamp}{suffix}";
    }

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
