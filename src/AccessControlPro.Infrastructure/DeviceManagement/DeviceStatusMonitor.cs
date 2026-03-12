using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;

namespace AccessControlPro.Infrastructure.DeviceManagement;

/// <summary>
/// Monitors device connectivity status in real-time.
/// Periodically pings devices and updates IsOnline status.
/// Prevents issues where admin thinks device is online but network is disconnected.
/// </summary>
public interface IDeviceStatusMonitor
{
    /// <summary>
    /// Start monitoring device connectivity (runs in background)
    /// </summary>
    void StartMonitoring(List<DeviceInfo> devices, int checkIntervalSeconds = 30);

    /// <summary>
    /// Stop monitoring
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// Manually check device connectivity
    /// </summary>
    Task<bool> IsDeviceOnlineAsync(DeviceInfo device, int timeoutMs = 5000);

    /// <summary>
    /// Get last known status
    /// </summary>
    Dictionary<string, (bool isOnline, DateTime lastChecked)> GetDeviceStatuses();
}

public class DeviceStatusMonitor : IDeviceStatusMonitor, IDisposable
{
    private readonly IAccessControlSdk _sdk;
    private readonly Dictionary<string, (bool isOnline, DateTime lastChecked)> _deviceStatuses = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _monitoringTask;
    private System.Net.NetworkInformation.Ping? _pinger;

    public DeviceStatusMonitor(IAccessControlSdk sdk)
    {
        _sdk = sdk;
    }

    public void StartMonitoring(List<DeviceInfo> devices, int checkIntervalSeconds = 30)
    {
        if (_monitoringTask?.IsCompleted == false)
            return; // Already monitoring

        _cancellationTokenSource = new CancellationTokenSource();
        _pinger = new System.Net.NetworkInformation.Ping();

        _monitoringTask = MonitoringLoopAsync(devices, checkIntervalSeconds, _cancellationTokenSource.Token);
    }

    public void StopMonitoring()
    {
        _cancellationTokenSource?.Cancel();
        _pinger?.Dispose();
    }

    public async Task<bool> IsDeviceOnlineAsync(DeviceInfo device, int timeoutMs = 5000)
    {
        try
        {
            // Try to ping device IP
            if (!System.Net.IPAddress.TryParse(device.IP, out var ipAddr))
                return false;

            using (var ping = new System.Net.NetworkInformation.Ping())
            {
                var reply = await ping.SendPingAsync(device.IP, timeoutMs);
                return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            }
        }
        catch
        {
            return false;
        }
    }

    public Dictionary<string, (bool isOnline, DateTime lastChecked)> GetDeviceStatuses()
    {
        lock (_deviceStatuses)
        {
            return new Dictionary<string, (bool, DateTime)>(_deviceStatuses);
        }
    }

    private async Task MonitoringLoopAsync(
        List<DeviceInfo> devices,
        int checkIntervalSeconds,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Check each device
                foreach (var device in devices)
                {
                    try
                    {
                        var isOnline = await IsDeviceOnlineAsync(device, 3000);
                        lock (_deviceStatuses)
                        {
                            _deviceStatuses[device.IP] = (isOnline, DateTime.UtcNow);
                        }
                    }
                    catch
                    {
                        // Ping failed, mark offline
                        lock (_deviceStatuses)
                        {
                            _deviceStatuses[device.IP] = (false, DateTime.UtcNow);
                        }
                    }
                }

                // Wait before next check
                await Task.Delay(TimeSpan.FromSeconds(checkIntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Cancellation token requested, exit loop
                break;
            }
            catch (Exception ex)
            {
                // Log error but continue monitoring
                System.Diagnostics.Debug.WriteLine($"[DeviceStatusMonitor] Error in monitoring loop: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(checkIntervalSeconds), cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        StopMonitoring();
        _cancellationTokenSource?.Dispose();
        _pinger?.Dispose();
    }
}
