namespace AccessControlPro.Application.Services;

public class CurrentUserService
{
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
    public bool IsLoggedIn => Username != null;
}
