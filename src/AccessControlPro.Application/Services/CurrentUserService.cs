namespace AccessControlPro.Application.Services;

public class CurrentUserService
{
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
    public bool IsLoggedIn => Username != null;

    private HashSet<string> _permissions = [];

    public void SetPermissions(string permissionsCsv)
    {
        _permissions = string.IsNullOrWhiteSpace(permissionsCsv)
            ? []
            : new HashSet<string>(permissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public bool HasPermission(string permission)
    {
        // Admin role always has all permissions
        if (Role == "Admin") return true;
        return _permissions.Contains(permission);
    }

    public bool HasAnyPermission(params string[] permissions)
    {
        if (Role == "Admin") return true;
        return permissions.Any(p => _permissions.Contains(p));
    }
}
