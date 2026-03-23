using System.Collections.Concurrent;

namespace AccessControlPro.Web.Services;

/// <summary>
/// Simple session state using a static store.
/// In production, use proper cookie-based auth.
/// For now: stores last login globally (single gym, few users).
/// </summary>
public class SessionState
{
    // Static store — survives circuit changes
    private static readonly ConcurrentDictionary<string, SessionData> _sessions = new();
    private static string _currentSessionId = "";

    // Language state
    public static string Language { get; set; } = "en"; // "en" or "ar"
    public static bool IsArabic => Language == "ar";
    public static string Dir => IsArabic ? "rtl" : "ltr";

    public static void ToggleLanguage()
    {
        Language = IsArabic ? "en" : "ar";
    }

    public bool IsAuthenticated => GetCurrent()?.IsAuthenticated ?? false;
    public string DisplayName => GetCurrent()?.DisplayName ?? "";
    public string Role => GetCurrent()?.Role ?? "";
    public int UserId => GetCurrent()?.UserId ?? 0;

    public bool IsOwner => Role == "Owner" || Role == "Admin";
    public bool IsPlayer => Role == "Player";
    public bool IsSuperAdmin => Role == "SuperAdmin";

    public void Login(AuthResult result)
    {
        var id = Guid.NewGuid().ToString("N");
        var data = new SessionData
        {
            IsAuthenticated = result.IsAuthenticated,
            DisplayName = result.DisplayName,
            Role = result.Role,
            UserId = result.UserId
        };
        _sessions[id] = data;
        _currentSessionId = id;
    }

    public void Logout()
    {
        if (!string.IsNullOrEmpty(_currentSessionId))
            _sessions.TryRemove(_currentSessionId, out _);
        _currentSessionId = "";
    }

    private static SessionData? GetCurrent()
    {
        if (string.IsNullOrEmpty(_currentSessionId)) return null;
        _sessions.TryGetValue(_currentSessionId, out var data);
        return data;
    }

    private class SessionData
    {
        public bool IsAuthenticated { get; set; }
        public string DisplayName { get; set; } = "";
        public string Role { get; set; } = "";
        public int UserId { get; set; }
    }
}
