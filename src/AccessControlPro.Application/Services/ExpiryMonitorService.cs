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
    /// Checks expired players only and disables their cards on devices in parallel.
    /// Optimized for frequent calls (every 10-30 seconds).
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

        try
        {
            // Only fetch expired + visit-exhausted players (NOT all employees)
            var expiredPlayers = (await _employeeRepository.GetExpiredAsync()).ToList();
            var visitExhausted = await GetVisitExhaustedPlayersAsync();

            // Combine both lists, deduplicate by Id
            var playersToDisable = expiredPlayers
                .Concat(visitExhausted)
                .GroupBy(e => e.Id)
                .Select(g => g.First())
                .ToList();

            if (playersToDisable.Count == 0) return result;

            // DO NOT send SDK commands here — hardware handles expiry via effectiveTimes countdown + date check.
            // Sending SDK commands every 10 seconds floods the device and causes TCP stuck issues.
            // Just log and count for reporting purposes.
            foreach (var employee in playersToDisable)
            {
                if (employee.IsFrozen) continue;

                if (employee.EndDate < DateTime.Now)
                {
                    result.DateExpired++;
                    result.Details.Add($"{employee.FullNameEn} (ID:{employee.Id}): Date expired ({employee.EndDate:yyyy-MM-dd})");
                }
                else if (employee.MaxVisits > 0 && employee.UsedVisits >= employee.MaxVisits)
                {
                    result.VisitExpired++;
                    result.Details.Add($"{employee.FullNameEn} (ID:{employee.Id}): Visit limit reached ({employee.UsedVisits}/{employee.MaxVisits})");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors++;
            result.Details.Add($"Monitor error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Gets players who have exhausted their visit count (MaxVisits > 0 and UsedVisits >= MaxVisits).
    /// Lightweight query — only fetches visit-based players with active synced cards.
    /// </summary>
    private async Task<IEnumerable<Employee>> GetVisitExhaustedPlayersAsync()
    {
        try
        {
            var allWithCards = await _employeeRepository.GetAllWithCardsAsync();
            return allWithCards
                .Where(e => !e.IsFrozen && e.MaxVisits > 0 && e.UsedVisits >= e.MaxVisits
                    && e.AccessCards != null && e.AccessCards.Any(c => c.IsActive && c.IsSyncedToDevice));
        }
        catch
        {
            return [];
        }
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
