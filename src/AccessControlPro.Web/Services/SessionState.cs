namespace AccessControlPro.Web.Services;

/// <summary>
/// Per-circuit session state. Registered as Scoped, so each Blazor circuit
/// (each browser tab) gets its own instance. Language is per-user, not global.
/// </summary>
public class SessionState
{
    // The website is Arabic-only as of 2026-05-13. The Language field, IsArabic property,
    // and Dir getter all remain so the dozens of existing @L("en","ar") helpers and dir=
    // attributes in razor pages keep compiling without a sweeping rewrite — they just
    // always render the Arabic branch / RTL. ToggleLanguage() is intentionally a no-op
    // so any leftover toggle button click does nothing visible.
    public string Language { get; set; } = "ar";
    public bool IsArabic => true;
    public string Dir => "rtl";

    public void ToggleLanguage()
    {
        // No-op — website is Arabic-only.
    }

    // Session data — INSTANCE fields (per circuit/user)
    private bool _isAuthenticated;
    private string _displayName = "";
    private string _role = "";
    private int _userId;
    private string _gymDatabase = "";
    private int _gymId;
    private DateTime _loginTime = DateTime.MinValue;

    public bool IsAuthenticated => _isAuthenticated && !IsSessionExpired;
    public string DisplayName => _displayName;
    public string Role => _role;
    public int UserId => _userId;
    public string GymDatabase => _gymDatabase;
    public int GymId => _gymId;

    public bool IsOwner => Role == "Owner" || Role == "Admin";
    public bool IsPlayer => Role == "Player";
    public bool IsSuperAdmin => Role == "SuperAdmin";

    // Session expires after 24 hours of inactivity
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromHours(24);
    public bool IsSessionExpired => _isAuthenticated && (DateTime.UtcNow - _loginTime) > SessionTimeout;

    public void Login(AuthResult result, string gymDatabase = "", int gymId = 0)
    {
        _isAuthenticated = result.IsAuthenticated;
        _displayName = result.DisplayName;
        _role = result.Role;
        _userId = result.UserId;
        _gymDatabase = gymDatabase;
        _gymId = gymId;
        _loginTime = DateTime.UtcNow;
    }

    public void RefreshSession()
    {
        if (_isAuthenticated)
            _loginTime = DateTime.UtcNow;
    }

    public void Logout()
    {
        _isAuthenticated = false;
        _displayName = "";
        _role = "";
        _userId = 0;
        _gymDatabase = "";
        _gymId = 0;
        _loginTime = DateTime.MinValue;
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