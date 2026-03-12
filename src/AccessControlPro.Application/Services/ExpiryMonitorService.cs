using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;

namespace AccessControlPro.Application.Services;

/// <summary>
/// Background service that monitors player subscriptions and automatically
/// disables expired cards on all devices.
///
/// Handles two types of expiry:
/// 1. Date-based: EndDate has passed (device also enforces via ValidTo, this is a safety net)
/// 2. Visit-count: UsedVisits >= MaxVisits (only server can enforce this)
/// </summary>
public interface IExpiryMonitorService
{
    /// <summary>
    /// Checks all players for expiry and disables cards on devices.
    /// Call this on a timer (every 2-5 minutes).
    /// </summary>
    Task<ExpiryCheckResult> CheckAndExpireAsync();
}

public class ExpiryCheckResult
{
    public int DateExpired { get; set; }
    public int VisitExpired { get; set; }
    public int AlreadyHandled { get; set; }
    public int Errors { get; set; }
    public List<string> Details { get; set; } = new();
}

public class ExpiryMonitorService : IExpiryMonitorService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IAccessCardRepository _cardRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ICardDeviceSyncRepository _syncRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IAccessControlSdk _sdk;

    public ExpiryMonitorService(
        IEmployeeRepository employeeRepository,
        IAccessCardRepository cardRepository,
        IDeviceRepository deviceRepository,
        ICardDeviceSyncRepository syncRepository,
        IAuditLogRepository auditLogRepository,
        IAccessControlSdk sdk)
    {
        _employeeRepository = employeeRepository;
        _cardRepository = cardRepository;
        _deviceRepository = deviceRepository;
        _syncRepository = syncRepository;
        _auditLogRepository = auditLogRepository;
        _sdk = sdk;
    }

    public async Task<ExpiryCheckResult> CheckAndExpireAsync()
    {
        var result = new ExpiryCheckResult();
        var now = DateTime.UtcNow;

        try
        {
            var allEmployees = await _employeeRepository.GetAllWithCardsAsync();
            var devices = (await _deviceRepository.GetAllAsync()).ToList();

            if (devices.Count == 0) return result;

            _sdk.Initialize();

            foreach (var employee in allEmployees)
            {
                if (employee.IsFrozen) continue; // Frozen players already handled

                var syncedCards = employee.AccessCards?
                    .Where(c => c.IsActive && c.IsSyncedToDevice)
                    .ToList();

                if (syncedCards == null || syncedCards.Count == 0) continue;

                bool shouldDisable = false;
                string reason = "";

                // Check date-based expiry
                if (employee.EndDate < now)
                {
                    shouldDisable = true;
                    reason = $"Date expired ({employee.EndDate:yyyy-MM-dd})";
                    result.DateExpired++;
                }
                // Check visit-count expiry
                else if (employee.MaxVisits > 0 && employee.UsedVisits >= employee.MaxVisits)
                {
                    shouldDisable = true;
                    reason = $"Visit limit reached ({employee.UsedVisits}/{employee.MaxVisits})";
                    result.VisitExpired++;
                }

                if (!shouldDisable) continue;

                // Disable cards on all devices
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

                            await _syncRepository.UpsertAsync(card.Id, device.Id, true);
                        }
                        catch (Exception ex)
                        {
                            await _syncRepository.UpsertAsync(card.Id, device.Id, false, ex.Message);
                            result.Errors++;
                        }
                    }
                }

                result.Details.Add($"{employee.FullNameEn} (ID:{employee.Id}): {reason}");

                await _auditLogRepository.AddAsync(new AuditLog
                {
                    Action = "AutoExpire",
                    EntityType = "Employee",
                    EntityId = employee.Id,
                    Details = $"Auto-expired: {reason}. Cards disabled on {devices.Count} devices.",
                    DetailsAr = $"انتهاء تلقائي: {reason}. تم تعطيل البطاقات على {devices.Count} جهاز.",
                    PerformedBy = "System",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            result.Errors++;
            result.Details.Add($"Monitor error: {ex.Message}");
        }

        return result;
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
}
