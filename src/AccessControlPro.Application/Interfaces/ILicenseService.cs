namespace AccessControlPro.Application.Interfaces;

public record LicenseStatus(
    bool IsValid,
    string Message,
    string MachineId,
    DateTime? ExpiryDate,
    int DaysRemaining = 0,
    string Tier = "Basic"); // Basic, Pro, Enterprise

public record DeveloperInfo(
    string CompanyName = "",
    string Phone = "",
    string Email = "",
    string WhatsApp = "");

public interface ILicenseService
{
    string GetMachineId();
    LicenseStatus CheckLicense();
    bool ActivateLicense(string licenseKey);
    /// <summary>Verifies a vendor-issued emergency offline-unlock code for this machine + month.</summary>
    bool VerifyOfflineUnlockCode(string code);
    static DeveloperInfo LoadDeveloperInfo()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return new DeveloperInfo();

        try
        {
            var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            if (!json.RootElement.TryGetProperty("Developer", out var dev))
                return new DeveloperInfo();

            return new DeveloperInfo(
                dev.TryGetProperty("CompanyName", out var cn) ? cn.GetString() ?? "" : "",
                dev.TryGetProperty("Phone", out var ph) ? ph.GetString() ?? "" : "",
                dev.TryGetProperty("Email", out var em) ? em.GetString() ?? "" : "",
                dev.TryGetProperty("WhatsApp", out var wa) ? wa.GetString() ?? "" : "");
        }
        catch { return new DeveloperInfo(); }
    }
}
