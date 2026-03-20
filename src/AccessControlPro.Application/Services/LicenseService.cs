using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccessControlPro.Application.Interfaces;
using Microsoft.Win32;

namespace AccessControlPro.Application.Services;

public class LicenseService : ILicenseService
{
    // HMAC secret key — the developer keeps this private
    // The KeyGen console tool uses the same key to generate valid codes
    private static readonly byte[] SecretKey = SHA256.HashData(
        Encoding.UTF8.GetBytes("ACP-2024-GymAccess-LicenseKey-X9K2M"));

    private static readonly DateTime Epoch = new(2024, 1, 1);
    private readonly string _licenseFilePath;

    // Registry path for tamper-proof backup (survives license.dat deletion)
    private const string RegistryPath = @"SOFTWARE\AccessControlPro";
    private const string RegPeakDate = "PD";        // highest date ever seen (obfuscated name)
    private const string RegActivatedAt = "AT";      // original activation date
    private const string RegMachineHash = "MH";      // hash to verify registry integrity

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

        // Layer 1: Check registry for tamper evidence even if license.dat was deleted
        var regTamper = CheckRegistryTamper(currentMachineId);
        if (regTamper != null)
            return new LicenseStatus(false, regTamper, currentMachineId, null);

        if (!File.Exists(_licenseFilePath))
            return new LicenseStatus(false, "NOT_ACTIVATED", currentMachineId, null);

        try
        {
            var encrypted = File.ReadAllBytes(_licenseFilePath);
            var json = DecryptData(encrypted);
            var data = JsonSerializer.Deserialize<LicenseFileData>(json);

            if (data == null)
                return new LicenseStatus(false, "INVALID_FILE", currentMachineId, null);

            // Check machine ID match
            if (!string.Equals(data.MachineId, currentMachineId, StringComparison.OrdinalIgnoreCase))
                return new LicenseStatus(false, "HARDWARE_CHANGED", currentMachineId, null);

            // Verify the license key signature
            var expiryDate = DateTime.Parse(data.ExpiryDate);
            if (!VerifyKey(data.MachineId, expiryDate, data.LicenseKey))
                return new LicenseStatus(false, "INVALID_KEY", currentMachineId, null);

            // Layer 2: Anti-clock-tamper using peak date (highest date ever seen)
            // This prevents gradual 1-day rollback attacks
            var peakDate = GetRegistryPeakDate();
            if (peakDate.HasValue && DateTime.Today < peakDate.Value.AddDays(-1))
                return new LicenseStatus(false, "CLOCK_TAMPER", currentMachineId, expiryDate);

            // Layer 3: Check against file's LastChecked too
            if (!string.IsNullOrEmpty(data.LastChecked))
            {
                var lastChecked = DateTime.Parse(data.LastChecked);
                if (DateTime.Today < lastChecked.AddDays(-1))
                    return new LicenseStatus(false, "CLOCK_TAMPER", currentMachineId, expiryDate);
            }

            // Layer 4: ActivatedAt guard — today must not be before activation date
            if (!string.IsNullOrEmpty(data.ActivatedAt))
            {
                var activatedAt = DateTime.Parse(data.ActivatedAt).Date;
                if (DateTime.Today < activatedAt.AddDays(-1))
                    return new LicenseStatus(false, "CLOCK_TAMPER", currentMachineId, expiryDate);
            }

            // Update peak date in file and registry (always moves forward, never backward)
            data.LastChecked = DateTime.Today.ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(data.PeakDate) || DateTime.Today > DateTime.Parse(data.PeakDate))
                data.PeakDate = DateTime.Today.ToString("yyyy-MM-dd");
            SaveLicenseFile(data);
            UpdateRegistryPeakDate(currentMachineId);

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

        // Anti-tamper: check if registry peak date is ahead of today (clock rolled back)
        var regPeak = GetRegistryPeakDate();
        if (regPeak.HasValue && DateTime.Today < regPeak.Value.AddDays(-1))
            return false; // clock has been set back — reject even valid keys

        // Save license file (encrypted)
        var data = new LicenseFileData
        {
            MachineId = machineId,
            ExpiryDate = expiryDate.Value.ToString("yyyy-MM-dd"),
            LicenseKey = cleanKey,
            ActivatedAt = DateTime.Today.ToString("yyyy-MM-dd"),
            LastChecked = DateTime.Today.ToString("yyyy-MM-dd"),
            PeakDate = DateTime.Today.ToString("yyyy-MM-dd")
        };
        SaveLicenseFile(data);

        // Write activation date + peak date to registry backup
        SaveRegistryActivation(machineId);
        UpdateRegistryPeakDate(machineId);

        return true;
    }

    /// <summary>
    /// Generates a license key for a given Machine ID.
    /// THIS METHOD IS USED BY THE DEVELOPER'S KEYGEN TOOL ONLY.
    /// </summary>
    internal static string GenerateKey(string machineId, int months = 6)
    {
        var expiryDate = DateTime.Today.AddMonths(months);
        return GenerateKeyForDate(machineId, expiryDate);
    }

    /// <summary>
    /// Generates a license key for a specific expiry date.
    /// </summary>
    internal static string GenerateKeyForDate(string machineId, DateTime expiryDate)
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

    #region Registry Anti-Tamper

    /// <summary>
    /// Checks registry for evidence of clock tampering.
    /// Even if user deletes license.dat, the registry remembers.
    /// Returns null if OK, or a status string if tampered.
    /// </summary>
    private string? CheckRegistryTamper(string machineId)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key == null) return null; // first install, no registry yet

            var peakStr = key.GetValue(RegPeakDate) as string;
            var hashStr = key.GetValue(RegMachineHash) as string;

            // Verify registry integrity (prevent user from editing registry values)
            if (!string.IsNullOrEmpty(peakStr) && !string.IsNullOrEmpty(hashStr))
            {
                var expectedHash = ComputeRegistryHash(machineId, peakStr);
                if (hashStr != expectedHash)
                    return null; // registry was tampered with — treat as fresh (no crash)

                if (DateTime.TryParse(peakStr, out var peakDate))
                {
                    if (DateTime.Today < peakDate.AddDays(-1))
                        return "CLOCK_TAMPER";
                }
            }
        }
        catch { } // registry access can fail — don't crash
        return null;
    }

    /// <summary>
    /// Gets the peak date from registry (highest date ever seen).
    /// </summary>
    private static DateTime? GetRegistryPeakDate()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            var peakStr = key?.GetValue(RegPeakDate) as string;
            if (DateTime.TryParse(peakStr, out var peakDate))
                return peakDate;
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Updates the peak date in registry. Only moves forward, never backward.
    /// </summary>
    private void UpdateRegistryPeakDate(string machineId)
    {
        try
        {
            var existingPeak = GetRegistryPeakDate();
            var newPeak = DateTime.Today;

            // Only update if today is newer than existing peak
            if (existingPeak.HasValue && existingPeak.Value >= newPeak)
                return;

            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            var peakStr = newPeak.ToString("yyyy-MM-dd");
            key.SetValue(RegPeakDate, peakStr);
            key.SetValue(RegMachineHash, ComputeRegistryHash(machineId, peakStr));
        }
        catch { }
    }

    /// <summary>
    /// Saves the activation date to registry on first activation.
    /// </summary>
    private static void SaveRegistryActivation(string machineId)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            // Only write if not already set (preserve original activation date)
            if (key.GetValue(RegActivatedAt) == null)
                key.SetValue(RegActivatedAt, DateTime.Today.ToString("yyyy-MM-dd"));
        }
        catch { }
    }

    /// <summary>
    /// HMAC hash of registry data to detect manual registry edits.
    /// </summary>
    private static string ComputeRegistryHash(string machineId, string peakDate)
    {
        var message = Encoding.UTF8.GetBytes($"REG|{machineId}|{peakDate}|ACP-Tamper-Guard");
        var hash = HMACSHA256.HashData(SecretKey, message);
        return Convert.ToHexString(hash, 0, 16);
    }

    #endregion

    private class LicenseFileData
    {
        public string MachineId { get; set; } = "";
        public string ExpiryDate { get; set; } = "";
        public string LicenseKey { get; set; } = "";
        public string ActivatedAt { get; set; } = "";
        public string LastChecked { get; set; } = "";
        public string PeakDate { get; set; } = "";
    }
}
