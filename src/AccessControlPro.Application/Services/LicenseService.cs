using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccessControlPro.Application.Interfaces;

namespace AccessControlPro.Application.Services;

public class LicenseService : ILicenseService
{
    // HMAC secret key — the developer keeps this private
    // The KeyGen console tool uses the same key to generate valid codes
    private static readonly byte[] SecretKey = SHA256.HashData(
        Encoding.UTF8.GetBytes("ACP-2024-GymAccess-LicenseKey-X9K2M"));

    private static readonly DateTime Epoch = new(2024, 1, 1);
    private readonly string _licenseFilePath;

    public LicenseService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "AccessControlPro");
        Directory.CreateDirectory(dir);
        _licenseFilePath = Path.Combine(dir, "license.dat");
    }

    /// <summary>
    /// Gets a unique Machine ID from motherboard + BIOS serial numbers.
    /// These never change unless the hardware is physically replaced.
    /// Format: XXXX-XXXX-XXXX-XXXX (16 hex chars)
    /// </summary>
    public string GetMachineId()
    {
        var hwId = GetHardwareFingerprint();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"ACP|{hwId}"));
        var hex = Convert.ToHexString(hash, 0, 8).ToUpperInvariant();
        return $"{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
    }

    /// <summary>
    /// Checks the current license status. Returns detailed info.
    /// </summary>
    public LicenseStatus CheckLicense()
    {
        var currentMachineId = GetMachineId();

        if (!File.Exists(_licenseFilePath))
            return new LicenseStatus(false, "NOT_ACTIVATED", currentMachineId, null);

        try
        {
            var encrypted = File.ReadAllBytes(_licenseFilePath);
            var json = DecryptData(encrypted);
            var data = JsonSerializer.Deserialize<LicenseFileData>(json);

            if (data == null)
                return new LicenseStatus(false, "INVALID_FILE", currentMachineId, null);

            // Check MAC address match
            if (!string.Equals(data.MachineId, currentMachineId, StringComparison.OrdinalIgnoreCase))
                return new LicenseStatus(false, "HARDWARE_CHANGED", currentMachineId, null);

            // Verify the license key signature
            var expiryDate = DateTime.Parse(data.ExpiryDate);
            if (!VerifyKey(data.MachineId, expiryDate, data.LicenseKey))
                return new LicenseStatus(false, "INVALID_KEY", currentMachineId, null);

            // Anti-clock-tamper: if current date is before last checked date, reject
            if (!string.IsNullOrEmpty(data.LastChecked))
            {
                var lastChecked = DateTime.Parse(data.LastChecked);
                if (DateTime.Today < lastChecked.AddDays(-1)) // 1-day tolerance
                    return new LicenseStatus(false, "CLOCK_TAMPER", currentMachineId, expiryDate);
            }

            // Update last checked date
            data.LastChecked = DateTime.Today.ToString("yyyy-MM-dd");
            SaveLicenseFile(data);

            // Check expiry
            var daysRemaining = (expiryDate - DateTime.Today).Days;
            if (daysRemaining < 0)
                return new LicenseStatus(false, "EXPIRED", currentMachineId, expiryDate, 0);

            return new LicenseStatus(true, "ACTIVE", currentMachineId, expiryDate, daysRemaining);
        }
        catch
        {
            return new LicenseStatus(false, "CORRUPTED", currentMachineId, null);
        }
    }

    /// <summary>
    /// Activates the software with a license key.
    /// The key is validated against the current machine ID.
    /// </summary>
    public bool ActivateLicense(string licenseKey)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            return false;

        var machineId = GetMachineId();
        var cleanKey = licenseKey.Replace("-", "").Replace(" ", "").ToUpperInvariant();

        // Decode the expiry date from the key
        var expiryDate = DecodeExpiryFromKey(cleanKey);
        if (expiryDate == null)
            return false;

        // Verify HMAC signature
        if (!VerifyKey(machineId, expiryDate.Value, cleanKey))
            return false;

        // Check that license is not already expired
        if (expiryDate.Value < DateTime.Today)
            return false;

        // Save license file (encrypted)
        var data = new LicenseFileData
        {
            MachineId = machineId,
            ExpiryDate = expiryDate.Value.ToString("yyyy-MM-dd"),
            LicenseKey = cleanKey,
            ActivatedAt = DateTime.UtcNow.ToString("o"),
            LastChecked = DateTime.Today.ToString("yyyy-MM-dd")
        };
        SaveLicenseFile(data);
        return true;
    }

    /// <summary>
    /// Generates a license key for a given Machine ID.
    /// THIS METHOD IS USED BY THE DEVELOPER'S KEYGEN TOOL ONLY.
    /// </summary>
    public static string GenerateKey(string machineId, int months = 6)
    {
        var expiryDate = DateTime.Today.AddMonths(months);
        return GenerateKeyForDate(machineId, expiryDate);
    }

    /// <summary>
    /// Generates a license key for a specific expiry date.
    /// </summary>
    public static string GenerateKeyForDate(string machineId, DateTime expiryDate)
    {
        // Encode expiry as days since epoch (2 bytes, big-endian)
        int days = (int)(expiryDate.Date - Epoch).TotalDays;
        if (days < 0 || days > 65535)
            throw new ArgumentException("Expiry date out of range.");

        // Build payload: 2 bytes expiry + 8 bytes HMAC
        var payload = new byte[10];
        payload[0] = (byte)(days >> 8);
        payload[1] = (byte)(days & 0xFF);

        // HMAC of machineId + expiryDate
        var message = Encoding.UTF8.GetBytes($"{machineId.ToUpperInvariant()}|{expiryDate:yyyyMMdd}");
        var hmac = HMACSHA256.HashData(SecretKey, message);
        Array.Copy(hmac, 0, payload, 2, 8);

        // Encode as hex: 10 bytes → 20 hex chars → XXXX-XXXX-XXXX-XXXX-XXXX
        var hex = Convert.ToHexString(payload).ToUpperInvariant();
        return $"{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}";
    }

    #region Private Helpers

    private static bool VerifyKey(string machineId, DateTime expiryDate, string cleanKey)
    {
        var expected = GenerateKeyForDate(machineId, expiryDate)
            .Replace("-", "").ToUpperInvariant();
        return string.Equals(expected, cleanKey, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? DecodeExpiryFromKey(string cleanKey)
    {
        if (cleanKey.Length != 20)
            return null;

        try
        {
            var bytes = Convert.FromHexString(cleanKey[..4]);
            int days = (bytes[0] << 8) | bytes[1];
            return Epoch.AddDays(days);
        }
        catch
        {
            return null;
        }
    }

    private static string GetHardwareFingerprint()
    {
        var motherboard = GetWmiValue("Win32_BaseBoard", "SerialNumber");
        var bios = GetWmiValue("Win32_BIOS", "SerialNumber");

        // Combine motherboard + BIOS serials for a stable fingerprint
        var combined = $"{motherboard}|{bios}";

        // If both are empty/generic, fall back to disk serial
        if (IsGenericSerial(motherboard) && IsGenericSerial(bios))
        {
            var disk = GetWmiValue("Win32_DiskDrive", "SerialNumber");
            combined = $"DISK|{disk}";
        }

        return string.IsNullOrWhiteSpace(combined) ? "NO-HW-ID" : combined;
    }

    private static string GetWmiValue(string wmiClass, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
            foreach (var obj in searcher.Get())
            {
                var val = obj[property]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(val))
                    return val;
            }
        }
        catch { }
        return "";
    }

    private static bool IsGenericSerial(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return true;
        var s = serial.Trim().ToUpperInvariant();
        return s is "" or "NONE" or "N/A" or "DEFAULT STRING"
            or "TO BE FILLED BY O.E.M." or "NOT AVAILABLE"
            or "0" or "00000000";
    }

    // Simple AES encryption for the license file (prevents casual editing)
    private static readonly byte[] FileKey = SHA256.HashData(
        Encoding.UTF8.GetBytes("ACP-FileEncryption-2024"));
    private static readonly byte[] FileIV = MD5.HashData(
        Encoding.UTF8.GetBytes("ACP-IV-2024-Salt"));

    private static byte[] EncryptData(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = FileKey;
        aes.IV = FileIV;
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
    }

    private static string DecryptData(byte[] cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = FileKey;
        aes.IV = FileIV;
        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private void SaveLicenseFile(LicenseFileData data)
    {
        var json = JsonSerializer.Serialize(data);
        var encrypted = EncryptData(json);
        File.WriteAllBytes(_licenseFilePath, encrypted);
    }

    #endregion

    private class LicenseFileData
    {
        public string MachineId { get; set; } = "";
        public string ExpiryDate { get; set; } = "";
        public string LicenseKey { get; set; } = "";
        public string ActivatedAt { get; set; } = "";
        public string LastChecked { get; set; } = "";
    }
}
