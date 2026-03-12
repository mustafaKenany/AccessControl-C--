namespace AccessControlPro.Infrastructure.DeviceManagement;

/// <summary>
/// Safe enum parsing and validation.
/// Prevents casting crashes when invalid enum values received (e.g., from hardware).
/// </summary>
public interface ISafeEnumParser
{
    /// <summary>
    /// Safely parse enum value and return default if invalid
    /// </summary>
    T SafeParse<T>(object value, T defaultValue) where T : struct, Enum;

    /// <summary>
    /// Check if value is valid enum member
    /// </summary>
    bool IsValidEnumValue<T>(object value) where T : struct, Enum;

    /// <summary>
    /// Get all valid enum values
    /// </summary>
    IEnumerable<T> GetValidValues<T>() where T : struct, Enum;

    /// <summary>
    /// Try parse with error info
    /// </summary>
    (bool success, object? value, string? error) TryParse<T>(object value) where T : struct, Enum;
}

public class SafeEnumParser : ISafeEnumParser
{
    public T SafeParse<T>(object value, T defaultValue) where T : struct, Enum
    {
        try
        {
            if (value == null)
                return defaultValue;

            // Try direct cast if already correct type
            if (value is T directly)
                return directly;

            // Try convert from int/string
            if (value is int intValue)
            {
                if (Enum.IsDefined(typeof(T), intValue))
                    return (T)Enum.ToObject(typeof(T), intValue);
            }

            if (value is string stringValue)
            {
                if (Enum.TryParse<T>(stringValue, ignoreCase: true, out var result))
                    return result;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[SafeEnumParser] Invalid {typeof(T).Name} value: {value}. Using default: {defaultValue}");

            return defaultValue;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SafeEnumParser] Error parsing {typeof(T).Name}: {ex.Message}. Using default: {defaultValue}");

            return defaultValue;
        }
    }

    public bool IsValidEnumValue<T>(object value) where T : struct, Enum
    {
        try
        {
            if (value == null)
                return false;

            if (value is T)
                return true;

            if (value is int intValue)
                return Enum.IsDefined(typeof(T), intValue);

            if (value is string stringValue)
                return Enum.TryParse<T>(stringValue, ignoreCase: true, out _);

            return false;
        }
        catch
        {
            return false;
        }
    }

    public IEnumerable<T> GetValidValues<T>() where T : struct, Enum
    {
        return Enum.GetValues(typeof(T)).Cast<T>();
    }

    public (bool success, object? value, string? error) TryParse<T>(object value) where T : struct, Enum
    {
        try
        {
            if (value == null)
                return (false, null, "Value cannot be null");

            if (value is T directly)
                return (true, directly, null);

            if (value is int intValue)
            {
                if (Enum.IsDefined(typeof(T), intValue))
                    return (true, (T)Enum.ToObject(typeof(T), intValue), null);
                else
                    return (false, null, $"Invalid {typeof(T).Name} value: {intValue}. Valid values: {string.Join(", ", GetValidValues<T>())}");
            }

            if (value is string stringValue)
            {
                if (Enum.TryParse(typeof(T), stringValue, ignoreCase: true, out var result))
                    return (true, result, null);
                else
                    return (false, null, $"Cannot parse '{stringValue}' as {typeof(T).Name}");
            }

            return (false, null, $"Cannot parse {value.GetType().Name} as {typeof(T).Name}");
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
}
