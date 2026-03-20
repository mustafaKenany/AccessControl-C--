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
}
