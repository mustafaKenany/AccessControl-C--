namespace AccessControlPro.Domain.Entities;

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Comma-separated list of permission keys (e.g. "AccessMainApp,Players.View,Players.Add")
    /// </summary>
    public string Permissions { get; set; } = string.Empty;
}
