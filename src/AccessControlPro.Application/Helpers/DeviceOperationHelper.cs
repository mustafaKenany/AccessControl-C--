using System.Net.NetworkInformation;
using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;

namespace AccessControlPro.Application.Helpers;

/// <summary>
/// Centralized helper for all device operations.
/// Enforces: Ping → PauseMonitor → SDK (sequential) → ResumeMonitor → DB update
/// </summary>
public class DeviceOperationHelper
{
    private readonly IAccessControlSdk _sdk;
    private static readonly object _sdkLock = new();

    public DeviceOperationHelper(IAccessControlSdk sdk)
    {
        _sdk = sdk;
    }

    /// <summary>
    /// Ping a device. Returns true if reachable within timeout.
    /// </summary>
    public static async Task<bool> PingAsync(string ip, int timeoutMs = 2000)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, timeoutMs);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ping multiple devices. Returns (online, offline) lists.
    /// </summary>
    public static async Task<(List<T> online, List<T> offline)> PingAllAsync<T>(
        IEnumerable<T> devices, Func<T, string> getIP, int timeoutMs = 2000)
    {
        var online = new List<T>();
        var offline = new List<T>();

        // Ping in parallel for speed (this is read-only, safe)
        var tasks = devices.Select(async d =>
        {
            var reachable = await PingAsync(getIP(d), timeoutMs);
            return (Device: d, IsOnline: reachable);
        });

        var results = await Task.WhenAll(tasks);
        foreach (var r in results)
        {
            if (r.IsOnline) online.Add(r.Device);
            else offline.Add(r.Device);
        }

        return (online, offline);
    }

    /// <summary>
    /// Execute an SDK operation with proper locking:
    /// StopMonitoring → Shutdown → wait for device TCP release → ReInitialize → execute → cleanup → RestartMonitoring
    /// </summary>
    public T ExecuteWithLock<T>(Func<T> operation)
    {
        lock (_sdkLock)
        {
            var wasMonitoring = StopAndReset();
            try
            {
                T result;
                try
                {
                    result = operation();
                }
                catch
                {
                    ExtendedReset();
                    result = operation();
                }
                CleanupAfterCall();
                return result;
            }
            finally
            {
                if (wasMonitoring) RestartMonitoring();
            }
        }
    }

    /// <summary>
    /// Execute an SDK operation (void) with proper locking.
    /// If first attempt fails, does extended reset (10s) and retries once.
    /// </summary>
    public void ExecuteWithLock(Action operation)
    {
        lock (_sdkLock)
        {
            var wasMonitoring = StopAndReset();
            try
            {
                try
                {
                    operation();
                }
                catch
                {
                    // First attempt failed — extended reset and retry
                    ExtendedReset();
                    operation(); // Retry once — if this fails too, exception propagates
                }
                CleanupAfterCall();
            }
            finally
            {
                if (wasMonitoring) RestartMonitoring();
            }
        }
    }

    /// <summary>
    /// Standard reset: Stop monitoring → Shutdown → short wait → ReInit.
    /// Network adapter reset handles TCP release, so only a short delay is needed here.
    /// </summary>
    private bool StopAndReset()
    {
        bool wasMonitoring = _lastMonitoredDevices != null;
        _sdk.StopMonitoring();
        _sdk.Shutdown();
        Thread.Sleep(500);
        _sdk.Initialize();
        return wasMonitoring;
    }

    /// <summary>
    /// Extended reset: longer 5s wait for retry after first attempt failure.
    /// </summary>
    private void ExtendedReset()
    {
        _sdk.StopMonitoring();
        _sdk.Shutdown();
        Thread.Sleep(1000);
        _sdk.Initialize();
    }

    /// <summary>
    /// Cleanup after SDK call — short pause only, no extra shutdown cycle.
    /// </summary>
    private void CleanupAfterCall()
    {
        Thread.Sleep(300);
    }

    /// <summary>
    /// Restarts monitoring with the last known device list.
    /// </summary>
    private void RestartMonitoring()
    {
        if (_lastMonitoredDevices != null && _lastMonitorCallback != null)
        {
            _sdk.StartMonitoring(_lastMonitoredDevices, _lastMonitorCallback);
        }
    }

    // Track monitoring state so we can restart after SDK reset
    private List<DeviceInfo>? _lastMonitoredDevices;
    private Action<SDK.Models.MonitorEvent>? _lastMonitorCallback;

    /// <summary>
    /// Call this when monitoring starts to track the devices/callback for auto-restart.
    /// </summary>
    public void SetMonitoringState(List<DeviceInfo>? devices, Action<SDK.Models.MonitorEvent>? callback)
    {
        _lastMonitoredDevices = devices;
        _lastMonitorCallback = callback;
    }

    /// <summary>
    /// Ping device, then execute SDK operation if online.
    /// Returns (success, errorMessage).
    /// </summary>
    public async Task<(bool success, string? error)> PingThenExecuteAsync(
        string ip, string deviceName, Action sdkOperation)
    {
        var reachable = await PingAsync(ip);
        if (!reachable)
            return (false, $"Device '{deviceName}' ({ip}) is offline");

        try
        {
            // Run blocking SDK operations on background thread to keep UI responsive
            await Task.Run(() => ExecuteWithLock(sdkOperation));
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Device '{deviceName}' ({ip}): {ex.Message}");
        }
    }

    /// <summary>
    /// Execute SDK card operation on multiple devices SEQUENTIALLY.
    /// Ping each device first. Single SDK session for all devices (no restart between).
    /// </summary>
    public async Task<DeviceOperationResult> ExecuteOnDevicesSequentialAsync(
        IEnumerable<(DeviceInfo info, string name, string ip, int dbId)> devices,
        Action<DeviceInfo> sdkAction)
    {
        var result = new DeviceOperationResult();
        var deviceList = devices.ToList();
        if (deviceList.Count == 0) return result;

        // Run entire blocking SDK batch on background thread to keep UI responsive
        await Task.Run(() =>
        {
        lock (_sdkLock)
        {
            var wasMonitoring = StopAndReset();

            try
            {
                foreach (var (info, name, ip, dbId) in deviceList)
                {
                    // 1. Ping (outside lock would be better but sequential is OK)
                    bool reachable;
                    try
                    {
                        using var ping = new System.Net.NetworkInformation.Ping();
                        var reply = ping.Send(ip, 2000);
                        reachable = reply.Status == System.Net.NetworkInformation.IPStatus.Success;
                    }
                    catch { reachable = false; }

                    if (!reachable)
                    {
                        result.AddFailed(dbId, name, ip, "Device offline");
                        continue;
                    }

                    // 2. Execute SDK call — retry once with extended reset if first attempt fails
                    try
                    {
                        try
                        {
                            sdkAction(info);
                        }
                        catch
                        {
                            // First attempt failed — extended reset and retry
                            ExtendedReset();
                            sdkAction(info);
                        }
                        CleanupAfterCall();
                        result.AddSuccess(dbId, name, ip);
                    }
                    catch (Exception ex)
                    {
                        result.AddFailed(dbId, name, ip, ex.Message);
                    }
                }
            }
            finally
            {
                if (wasMonitoring) RestartMonitoring();
            }
        }
        }); // End Task.Run

        return result;
    }
}

/// <summary>
/// Result of a multi-device operation.
/// </summary>
public class DeviceOperationResult
{
    public List<DeviceResult> Succeeded { get; } = new();
    public List<DeviceResult> Failed { get; } = new();

    public int SuccessCount => Succeeded.Count;
    public int FailedCount => Failed.Count;
    public int TotalCount => SuccessCount + FailedCount;
    public bool AllSucceeded => FailedCount == 0;
    public bool AllFailed => SuccessCount == 0 && FailedCount > 0;

    public void AddSuccess(int deviceId, string name, string ip)
        => Succeeded.Add(new DeviceResult(deviceId, name, ip, null));

    public void AddFailed(int deviceId, string name, string ip, string error)
        => Failed.Add(new DeviceResult(deviceId, name, ip, error));

    public string GetSummary()
    {
        var msg = $"Success: {SuccessCount}/{TotalCount}";
        if (Failed.Count > 0)
            msg += "\n\nFailed:\n" + string.Join("\n", Failed.Select(f => $"  - {f.Name} ({f.IP}): {f.Error}"));
        return msg;
    }
}

public record DeviceResult(int DeviceId, string Name, string IP, string? Error);
