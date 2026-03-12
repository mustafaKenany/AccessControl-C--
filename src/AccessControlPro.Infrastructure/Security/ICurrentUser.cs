namespace AccessControlPro.Infrastructure.Security;

/// <summary>
/// Current user abstraction for Infrastructure layer.
/// Prevents circular dependency between Infrastructure and Application layers.
/// </summary>
public interface ICurrentUser
{
    string? Username { get; }
    string? Role { get; }
    bool HasPermission(string permission);
    IEnumerable<string> GetPermissions();
}

/// <summary>
/// Adapter to convert CurrentUserService from Application layer
/// </summary>
public class CurrentUserAdapter : ICurrentUser
{
    private readonly dynamic _currentUserService;

    public string? Username => _currentUserService?.Username;
    public string? Role => _currentUserService?.Role;

    public CurrentUserAdapter(dynamic currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public bool HasPermission(string permission)
    {
        return _currentUserService?.HasPermission(permission) ?? false;
    }

    public IEnumerable<string> GetPermissions()
    {
        // CurrentUserService doesn't expose this directly, so Admin gets all
        if (Role == "Admin")
            return AccessControlPro.Domain.Enums.AppPermission.All;
        return [];
    }
}
