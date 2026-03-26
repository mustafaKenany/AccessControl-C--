namespace AccessControlPro.Web.Services;

/// <summary>
/// Per-circuit session state. Registered as Scoped, so each Blazor circuit
/// (each browser tab) gets its own instance. No static session data.
/// </summary>
public class SessionState
{
    // Language is static (shared across all users — OK for a single-gym deployment)
    public static string Language { get; set; } = "en";
    public static bool IsArabic => Language == "ar";
    public static string Dir => IsArabic ? "rtl" : "ltr";

    public static void ToggleLanguage()
    {
        Language = IsArabic ? "en" : "ar";
    }

    // Session data — INSTANCE fields (per circuit/user)
    private bool _isAuthenticated;
    private string _displayName = "";
    private string _role = "";
    private int _userId;
    private string _gymDatabase = "";
    private int _gymId;

    public bool IsAuthenticated => _isAuthenticated;
    public string DisplayName => _displayName;
    public string Role => _role;
    public int UserId => _userId;
    public string GymDatabase => _gymDatabase;
    public int GymId => _gymId;

    public bool IsOwner => Role == "Owner" || Role == "Admin";
    public bool IsPlayer => Role == "Player";
    public bool IsSuperAdmin => Role == "SuperAdmin";

    public void Login(AuthResult result, string gymDatabase = "", int gymId = 0)
    {
        _isAuthenticated = result.IsAuthenticated;
        _displayName = result.DisplayName;
        _role = result.Role;
        _userId = result.UserId;
        _gymDatabase = gymDatabase;
        _gymId = gymId;
    }

    public void Logout()
    {
        _isAuthenticated = false;
        _displayName = "";
        _role = "";
        _userId = 0;
        _gymDatabase = "";
        _gymId = 0;
    }

    // These methods are used by SuperAdmin gym management.
    // With instance-based sessions, we can't clear other circuits' state.
    // The gym active check on login will prevent re-access to deactivated/deleted gyms.
    public static void ClearSessionsForGym(int gymId)
    {
        // No-op: each circuit manages its own state.
        // Deactivated/deleted gyms are blocked at login time via the IsActive check.
    }

    public static void ClearAllSessions()
    {
        // No-op: each circuit manages its own state.
    }
}
