namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Cached gym identity (name / phone / address) used as the header on printed
/// receipts, renewal slips and member ID cards. Populated once at login from
/// AppSettings (see App.OnStartup) and refreshed whenever settings are saved.
/// Kept as a static holder so lightweight print dialogs can read it without DI.
/// </summary>
public static class GymProfile
{
    public static string GymName { get; set; } = "";
    public static string Phone { get; set; } = "";
    public static string Address { get; set; } = "";
    public static string CompanyName { get; set; } = "";

    /// <summary>Gym name to print, falling back to a neutral label when unset.</summary>
    public static string DisplayName =>
        !string.IsNullOrWhiteSpace(GymName) ? GymName.Trim()
        : !string.IsNullOrWhiteSpace(CompanyName) ? CompanyName.Trim()
        : "GYM";

    public static void Update(Domain.Entities.AppSettings? s)
    {
        if (s == null) return;
        GymName = s.GymName ?? "";
        Phone = s.Phone ?? "";
        Address = s.Address ?? "";
        CompanyName = s.CompanyName ?? "";
    }
}
