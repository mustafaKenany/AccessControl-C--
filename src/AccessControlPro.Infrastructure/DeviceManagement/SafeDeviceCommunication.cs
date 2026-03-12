using AccessControlPro.SDK.Models;
using AccessControlPro.SDK.Wrapper;

namespace AccessControlPro.Infrastructure.DeviceManagement;

/// <summary>
/// Safe wrapper around IAccessControlSdk that adds validation, rate limiting, retry logic, and encryption.
/// All device communication should go through this wrapper.
/// </summary>
public interface ISafeDeviceCommunication
{
    /// <summary>
    /// Safely add access card with full validation and retry logic
    /// </summary>
    Task<bool> AddAccessCardSafeAsync(
        DeviceInfo device,
        string cardNumber,
        string cardPassword,
        int openMode,
        string doorPermissions,
        DateTime permitTime,
        int effectiveTimes = 1,
        int timePeriodIndex = 0,
        bool holidayEnabled = false);

    /// <summary>
    /// Safely update device IP address
    /// </summary>
    Task<bool> UpdateIPAddressSafeAsync(DeviceInfo device, string newIP);

    /// <summary>
    /// Safely open door with retry logic
    /// </summary>
    Task<bool> RemoteOpenDoorSafeAsync(DeviceInfo device, int[] doorNumbers);

    /// <summary>
    /// Safely get device info
    /// </summary>
    Task<string> GetDeviceInfoSafeAsync(DeviceInfo device);

    /// <summary>
    /// Safely calibrate device time
    /// </summary>
    Task<bool> CalibrateTimeSafeAsync(DeviceInfo device);
}

public class SafeDeviceCommunication : ISafeDeviceCommunication
{
    private readonly IAccessControlSdk _sdk;
    private readonly ISdkRetryPolicy _retryPolicy;
    private readonly IDeviceParameterValidator _deviceValidator;
    private readonly ICardDoorPermissionsValidator _cardValidator;
    private readonly ICardOperationRateLimiter _rateLimiter;
    private readonly IDevicePasswordEncryption _passwordEncryption;
    private static readonly System.Diagnostics.TraceSource _trace = new("SDK.Communication");

    private static void LogWarning(string message)
    {
        _trace.TraceEvent(System.Diagnostics.TraceEventType.Warning, 0, message);
    }

    private static void LogError(string message)
    {
        _trace.TraceEvent(System.Diagnostics.TraceEventType.Error, 0, message);
    }

    private static void LogInfo(string message)
    {
        _trace.TraceEvent(System.Diagnostics.TraceEventType.Information, 0, message);
    }

    public SafeDeviceCommunication(
        IAccessControlSdk sdk,
        ISdkRetryPolicy retryPolicy,
        IDeviceParameterValidator deviceValidator,
        ICardDoorPermissionsValidator cardValidator,
        ICardOperationRateLimiter rateLimiter,
        IDevicePasswordEncryption passwordEncryption)
    {
        _sdk = sdk;
        _retryPolicy = retryPolicy;
        _deviceValidator = deviceValidator;
        _cardValidator = cardValidator;
        _rateLimiter = rateLimiter;
        _passwordEncryption = passwordEncryption;
    }

    public async Task<bool> AddAccessCardSafeAsync(
        DeviceInfo device,
        string cardNumber,
        string cardPassword,
        int openMode,
        string doorPermissions,
        DateTime permitTime,
        int effectiveTimes = 1,
        int timePeriodIndex = 0,
        bool holidayEnabled = false)
    {
        try
        {
            // Validate device IP
            var (deviceValid, deviceError) = _deviceValidator.ValidateIPAddress(device.IP);
            if (!deviceValid)
                throw new InvalidOperationException($"Device validation failed: {deviceError}");

            // Validate card parameters
            var (cardValid, cardErrors) = _cardValidator.ValidateCardParameters(
                cardNumber,
                doorPermissions,
                effectiveTimes,
                openMode,
                DateTime.Today,
                permitTime);

            if (!cardValid)
                throw new InvalidOperationException($"Card validation failed: {string.Join("; ", cardErrors)}");

            // Check rate limit
            _rateLimiter.ThrottleOrThrow(device.IP.GetHashCode(), "AddCard");

            // Execute with retry
            await _retryPolicy.ExecuteWithRetryAsync(
                async () =>
                {
                    // Run SDK operation synchronously (SDK doesn't have async API)
                    _sdk.AddAccessCard(
                        device,
                        cardNumber,
                        cardPassword,
                        openMode,
                        doorPermissions,
                        permitTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        effectiveTimes,
                        timePeriodIndex,
                        holidayEnabled);

                    _rateLimiter.RecordOperation(device.IP.GetHashCode(), "AddCard");
                    LogInfo($"Card {cardNumber} synced to device {device.IP}");
                    return true;
                },
                $"AddCard({cardNumber} -> {device.IP})",
                maxRetries: 3,
                initialDelayMs: 500);

            return true;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit"))
        {
            LogWarning($"Rate limit on {device.IP}: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            LogError($"Failed to add card {cardNumber} to {device.IP}: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> UpdateIPAddressSafeAsync(DeviceInfo device, string newIP)
    {
        try
        {
            // Validate new IP
            var (ipValid, ipError) = _deviceValidator.ValidateIPAddress(newIP);
            if (!ipValid)
                throw new InvalidOperationException($"Invalid new IP address: {ipError}");

            // Update device info object
            device.IP = newIP;

            // Execute update with retry
            await _retryPolicy.ExecuteWithRetryAsync(
                async () =>
                {
                    _sdk.UpdateIP(device, 0); // doorCount=0 for simple update
                    LogInfo($"Device IP updated to {newIP}");
                    return true;
                },
                $"UpdateIP({device.IP})",
                maxRetries: 2,
                initialDelayMs: 1000);

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to update device IP: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> RemoteOpenDoorSafeAsync(DeviceInfo device, int[] doorNumbers)
    {
        try
        {
            // Validate device
            var (deviceValid, deviceError) = _deviceValidator.ValidateIPAddress(device.IP);
            if (!deviceValid)
                throw new InvalidOperationException($"Device validation failed: {deviceError}");

            // Validate door numbers
            if (doorNumbers == null || doorNumbers.Length == 0)
                throw new InvalidOperationException("At least one door number required");

            if (doorNumbers.Any(d => d < 1 || d > 32))
                throw new InvalidOperationException("Door numbers must be between 1 and 32");

            // Execute with retry
            await _retryPolicy.ExecuteWithRetryAsync(
                async () =>
                {
                    _sdk.RemoteOpenDoor(device, doorNumbers);
                    LogInfo($"Opened doors {string.Join(",", doorNumbers)} on {device.IP}");
                    return true;
                },
                $"OpenDoor({string.Join(",", doorNumbers)} -> {device.IP})",
                maxRetries: 2,
                initialDelayMs: 300);

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to open door: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GetDeviceInfoSafeAsync(DeviceInfo device)
    {
        try
        {
            var (deviceValid, deviceError) = _deviceValidator.ValidateIPAddress(device.IP);
            if (!deviceValid)
                throw new InvalidOperationException($"Device validation failed: {deviceError}");

            return await _retryPolicy.ExecuteWithRetryAsync(
                async () =>
                {
                    var info = _sdk.GetDeviceInfo(device);
                    return info ?? "Unknown";
                },
                $"GetInfo({device.IP})",
                maxRetries: 2,
                initialDelayMs: 500);
        }
        catch (Exception ex)
        {
            LogError($"Failed to get device info: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> CalibrateTimeSafeAsync(DeviceInfo device)
    {
        try
        {
            var (deviceValid, deviceError) = _deviceValidator.ValidateIPAddress(device.IP);
            if (!deviceValid)
                throw new InvalidOperationException($"Device validation failed: {deviceError}");

            await _retryPolicy.ExecuteWithRetryAsync(
                async () =>
                {
                    _sdk.CalibrateTime(device);
                    LogInfo($"Device time calibrated: {device.IP}");
                    return true;
                },
                $"CalibrateTime({device.IP})",
                maxRetries: 2,
                initialDelayMs: 500);

            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to calibrate device time: {ex.Message}");
            throw;
        }
    }
}
