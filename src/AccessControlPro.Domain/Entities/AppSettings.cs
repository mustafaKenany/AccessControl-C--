namespace AccessControlPro.Domain.Entities;

public class AppSettings
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = "";
    public string GymName { get; set; } = "";
    public string LogoPath { get; set; } = "";
    public string DevLogoPath { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    /// <summary>Gym owner name — printed on receipts.</summary>
    public string Owner { get; set; } = "";
    /// <summary>This gym's cloud API key, mirrored from appsettings.json so a fresh reinstall
    /// (which keeps the database) can recover the SAME key instead of generating a new one
    /// that the cloud wouldn't recognize.</summary>
    public string CloudApiKey { get; set; } = "";
}
