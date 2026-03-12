using AccessControlPro.SDK.Models;

namespace AccessControlPro.Infrastructure.DeviceManagement;

/// <summary>
/// Extension methods for easier SDK parameter validation
/// Usage: device.ValidateNetworkConfig() instead of validator.ValidateDeviceNetworkConfig(...)
/// </summary>
public static class DeviceValidationExtensions
{
    public static (bool isValid, List<string> errors) ValidateNetworkConfig(
        this DeviceInfo device,
        IDeviceParameterValidator validator)
    {
        return validator.ValidateDeviceNetworkConfig(
            device.IP,
            device.TCPPort,
            device.UDPPort,
            device.Gateway,
            device.SubnetMask,
            device.MAC);
    }

    public static (bool isValid, List<string> errors) ValidateNetworkConfig(
        this Domain.Entities.Device device,
        IDeviceParameterValidator validator)
    {
        return validator.ValidateDeviceNetworkConfig(
            device.IP,
            device.TCPPort,
            device.UDPPort,
            device.Gateway,
            device.SubnetMask,
            device.MAC);
    }

    public static bool IsValidIP(this string ipAddress, IDeviceParameterValidator validator)
    {
        var (isValid, _) = validator.ValidateIPAddress(ipAddress);
        return isValid;
    }

    public static bool IsValidPort(this int port, IDeviceParameterValidator validator)
    {
        var (isValid, _) = validator.ValidatePort(port);
        return isValid;
    }

    public static bool IsValidSubnetMask(this string mask, IDeviceParameterValidator validator)
    {
        var (isValid, _) = validator.ValidateSubnetMask(mask);
        return isValid;
    }

    public static bool IsValidMAC(this string mac, IDeviceParameterValidator validator)
    {
        var (isValid, _) = validator.ValidateMACAddress(mac);
        return isValid;
    }
}

/// <summary>
/// Extension methods for card validation
/// </summary>
public static class CardValidationExtensions
{
    public static (bool isValid, string? error) IsValidDoorPermissions(
        this string permissions,
        ICardDoorPermissionsValidator validator)
    {
        return validator.ValidateDoorPermissions(permissions);
    }

    public static (bool isValid, string? error) IsValidEffectiveTimes(
        this int times,
        ICardDoorPermissionsValidator validator)
    {
        return validator.ValidateEffectiveTimes(times);
    }

    public static (bool isValid, string? error) IsValidCardValidity(
        this (DateTime validFrom, DateTime validTo) dates,
        ICardDoorPermissionsValidator validator)
    {
        return validator.ValidateCardValidity(dates.validFrom, dates.validTo);
    }

    public static (bool isValid, string? error) IsValidOpenMode(
        this int mode,
        ICardDoorPermissionsValidator validator)
    {
        return validator.ValidateOpenMode(mode);
    }
}

/// <summary>
/// Retry policy extensions for more readable code
/// </summary>
public static class RetryPolicyExtensions
{
    public static async Task<T> WithRetryAsync<T>(
        this Func<Task<T>> operation,
        ISdkRetryPolicy retryPolicy,
        string operationName)
    {
        return await retryPolicy.ExecuteWithRetryAsync(operation, operationName);
    }

    public static T WithRetry<T>(
        this Func<T> operation,
        ISdkRetryPolicy retryPolicy,
        string operationName)
    {
        return retryPolicy.ExecuteWithRetry(operation, operationName);
    }
}
