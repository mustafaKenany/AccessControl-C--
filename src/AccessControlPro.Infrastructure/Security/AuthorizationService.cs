using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Infrastructure.Security;

/// <summary>
/// Server-side authorization enforcement.
/// Delegates to per-user granular permissions stored on AppUser.Permissions (CSV).
/// </summary>
public interface IAuthorizationService
{
    bool HasPermission(string permissionName);
    void ThrowIfNoPermission(string permissionName, string operationName);
    IEnumerable<string> GetUserPermissions();
    string GetUserRole();
}

public class AuthorizationService : IAuthorizationService
{
    private readonly ICurrentUser _currentUser;

    public AuthorizationService(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public bool HasPermission(string permissionName)
    {
        if (_currentUser.Username == null)
            return false;

        return _currentUser.HasPermission(permissionName);
    }

    public void ThrowIfNoPermission(string permissionName, string operationName)
    {
        if (!HasPermission(permissionName))
            throw new UnauthorizedAccessException(
                $"User '{_currentUser.Username}' does not have permission '{permissionName}' to {operationName}");
    }

    public IEnumerable<string> GetUserPermissions()
    {
        if (_currentUser.Username == null)
            return [];

        if (_currentUser.Role == "Admin")
            return AppPermission.All;

        return _currentUser.GetPermissions();
    }

    public string GetUserRole()
    {
        return _currentUser.Role ?? "User";
    }
}

/// <summary>
/// Extension methods for common permission checks using AppPermission constants.
/// </summary>
public static class AuthorizationExtensions
{
    // Players
    public static bool CanViewPlayers(this IAuthorizationService auth) => auth.HasPermission(AppPermission.PlayersView);
    public static bool CanAddPlayer(this IAuthorizationService auth) => auth.HasPermission(AppPermission.PlayersAdd);
    public static bool CanEditPlayer(this IAuthorizationService auth) => auth.HasPermission(AppPermission.PlayersEdit);
    public static bool CanDeletePlayer(this IAuthorizationService auth) => auth.HasPermission(AppPermission.PlayersDelete);
    public static bool CanAssignCard(this IAuthorizationService auth) => auth.HasPermission(AppPermission.PlayersAssignCard);
    public static bool CanRemoveCard(this IAuthorizationService auth) => auth.HasPermission(AppPermission.PlayersRemoveCard);
    public static bool CanFreezePlayer(this IAuthorizationService auth) => auth.HasPermission(AppPermission.PlayersFreeze);
    public static bool CanRenewPlayer(this IAuthorizationService auth) => auth.HasPermission(AppPermission.PlayersRenew);

    // Devices
    public static bool CanViewDevices(this IAuthorizationService auth) => auth.HasPermission(AppPermission.DevicesView);
    public static bool CanAddDevice(this IAuthorizationService auth) => auth.HasPermission(AppPermission.DevicesAdd);
    public static bool CanEditDevice(this IAuthorizationService auth) => auth.HasPermission(AppPermission.DevicesEdit);
    public static bool CanDeleteDevice(this IAuthorizationService auth) => auth.HasPermission(AppPermission.DevicesDelete);
    public static bool CanConnectDevice(this IAuthorizationService auth) => auth.HasPermission(AppPermission.DevicesConnect);

    // Doors
    public static bool CanViewDoors(this IAuthorizationService auth) => auth.HasPermission(AppPermission.DoorsView);
    public static bool CanOpenCloseDoor(this IAuthorizationService auth) => auth.HasPermission(AppPermission.DoorsOpenClose);
    public static bool CanManageDoorSettings(this IAuthorizationService auth) => auth.HasPermission(AppPermission.DoorsSettings);

    // Finance
    public static bool CanViewFinance(this IAuthorizationService auth) => auth.HasPermission(AppPermission.FinanceView);
    public static bool CanManageFinance(this IAuthorizationService auth) => auth.HasPermission(AppPermission.FinanceManage);

    // Cash Flow
    public static bool CanViewCashFlow(this IAuthorizationService auth) => auth.HasPermission(AppPermission.CashFlowView);
    public static bool CanManageCashFlow(this IAuthorizationService auth) => auth.HasPermission(AppPermission.CashFlowManage);

    // Require (throw) versions
    public static void RequirePlayerView(this IAuthorizationService auth) => auth.ThrowIfNoPermission(AppPermission.PlayersView, "view players");
    public static void RequirePlayerAdd(this IAuthorizationService auth) => auth.ThrowIfNoPermission(AppPermission.PlayersAdd, "add player");
    public static void RequirePlayerEdit(this IAuthorizationService auth) => auth.ThrowIfNoPermission(AppPermission.PlayersEdit, "edit player");
    public static void RequirePlayerDelete(this IAuthorizationService auth) => auth.ThrowIfNoPermission(AppPermission.PlayersDelete, "delete player");
    public static void RequireCardAssign(this IAuthorizationService auth) => auth.ThrowIfNoPermission(AppPermission.PlayersAssignCard, "assign card");
    public static void RequireDeviceConnect(this IAuthorizationService auth) => auth.ThrowIfNoPermission(AppPermission.DevicesConnect, "connect device");
    public static void RequireDeviceDelete(this IAuthorizationService auth) => auth.ThrowIfNoPermission(AppPermission.DevicesDelete, "delete device");
}
