namespace AccessControlPro.Web.Services;

/// <summary>
/// Scoped session state for tracking the currently logged-in user per circuit.
/// </summary>
public class SessionState
{
    public bool IsAuthenticated { get; set; }
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = ""; // "Owner" or "Player"
    public int UserId { get; set; }

    public bool IsOwner => Role == "Owner";
    public bool IsPlayer => Role == "Player";

    public void Login(AuthResult result)
    {
        IsAuthenticated = result.IsAuthenticated;
        DisplayName = result.DisplayName;
        Role = result.Role;
        UserId = result.UserId;
    }

    public void Logout()
    {
        IsAuthenticated = false;
        DisplayName = "";
        Role = "";
        UserId = 0;
    }
}
