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
            // PHOTO-FREE lightweight query. This runs every ~10 s; the old path loaded every player's
            // full JPEG (PhotoData byte[]) + card graph, tracked, each cycle → ~108 MB/min of dead photo
            // churn that fragmented the 32-bit heap into OutOfMemoryException. We only COUNT + log for the
            // status report (the hardware itself enforces expiry via the effectiveTimes countdown + date).
            var rows = await _employeeRepository.GetExpiryMonitorRowsAsync();

            foreach (var r in rows)
            {
                if (r.IsFrozen) continue;

                if (r.EndDate < DateTime.Now)
                {
                    result.DateExpired++;
                    result.Details.Add($"{r.FullNameEn} (ID:{r.Id}): Date expired ({r.EndDate:yyyy-MM-dd})");
                }
                else if (r.MaxVisits > 0 && r.UsedVisits >= r.MaxVisits && r.HasActiveSyncedCard)
                {
                    result.VisitExpired++;
                    result.Details.Add($"{r.FullNameEn} (ID:{r.Id}): Visit limit reached ({r.UsedVisits}/{r.MaxVisits})");
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
