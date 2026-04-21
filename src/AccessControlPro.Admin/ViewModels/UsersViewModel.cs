using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.Admin.ViewModels;

public partial class UsersViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ISessionLogger _sessionLogger;
    private readonly CurrentUserService _currentUser;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isAddingNew;
    [ObservableProperty] private AppUser? _selectedUser;

    // Edit form fields
    [ObservableProperty] private string _editUsername = string.Empty;
    [ObservableProperty] private string _editDisplayName = string.Empty;
    [ObservableProperty] private string _editRole = "User";
    [ObservableProperty] private bool _editIsActive = true;
    [ObservableProperty] private string _editPassword = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;

    // Search
    [ObservableProperty] private string _searchText = string.Empty;

    // Stats
    [ObservableProperty] private int _totalUsers;
    [ObservableProperty] private int _activeUsers;
    [ObservableProperty] private int _inactiveUsers;

    public ICollectionView UsersView { get; }
    public ObservableCollection<AppUser> Users { get; } = new();
    public ObservableCollection<PermissionItem> EditPermissions { get; } = new();

    public string[] AvailableRoles { get; } = ["Admin", "User"];

    public UsersViewModel(IAuthService authService, IAuditLogRepository auditLogRepository,
        ISessionLogger sessionLogger, CurrentUserService currentUser)
    {
        _authService = authService;
        _auditLogRepository = auditLogRepository;
        _sessionLogger = sessionLogger;
        _currentUser = currentUser;

        UsersView = CollectionViewSource.GetDefaultView(Users);
        UsersView.Filter = FilterUsers;

        InitializePermissionItems();

        // Re-initialize permission display names when language switches
        Lang.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Lang.PermPermissions))
                RefreshPermissionDisplayNames();
        };
    }

    partial void OnSearchTextChanged(string value)
    {
        UsersView.Refresh();
    }

    private bool FilterUsers(object obj)
    {
        if (obj is not AppUser user) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        var search = SearchText.Trim().ToLowerInvariant();
        return (user.Username?.ToLowerInvariant().Contains(search) ?? false) ||
               (user.DisplayName?.ToLowerInvariant().Contains(search) ?? false) ||
               (user.Role?.ToLowerInvariant().Contains(search) ?? false);
    }

    private void UpdateStats()
    {
        TotalUsers = Users.Count;
        ActiveUsers = Users.Count(u => u.IsActive);
        InactiveUsers = Users.Count(u => !u.IsActive);
    }

    private void InitializePermissionItems()
    {
        EditPermissions.Clear();
        foreach (var (groupKey, permissions) in AppPermission.Groups)
        {
            foreach (var perm in permissions)
            {
                EditPermissions.Add(new PermissionItem
                {
                    Key = perm,
                    GroupName = Lang.GetPermissionGroupName(groupKey),
                    DisplayName = Lang.GetPermissionDisplayName(perm),
                    IsChecked = false
                });
            }
        }
    }

    private void RefreshPermissionDisplayNames()
    {
        foreach (var item in EditPermissions)
        {
            item.DisplayName = Lang.GetPermissionDisplayName(item.Key);
        }
    }

    private void LoadPermissionsFromCsv(string csv)
    {
        var set = string.IsNullOrWhiteSpace(csv)
            ? new HashSet<string>()
            : new HashSet<string>(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        foreach (var item in EditPermissions)
            item.IsChecked = set.Contains(item.Key);
    }

    private string GetPermissionsCsv()
    {
        return string.Join(",", EditPermissions.Where(p => p.IsChecked).Select(p => p.Key));
    }

    [RelayCommand]
    private void SelectAllPermissions()
    {
        foreach (var item in EditPermissions)
            item.IsChecked = true;
    }

    [RelayCommand]
    private void ClearAllPermissions()
    {
        foreach (var item in EditPermissions)
            item.IsChecked = false;
    }

    public async Task LoadUsersAsync()
    {
        IsLoading = true;
        try
        {
            var users = await _authService.GetAllUsersAsync();
            Users.Clear();
            foreach (var u in users)
                Users.Add(u);
            UpdateStats();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void StartAddUser()
    {
        IsAddingNew = true;
        IsEditing = true;
        SelectedUser = null;
        EditUsername = string.Empty;
        EditDisplayName = string.Empty;
        EditRole = "User";
        EditIsActive = true;
        EditPassword = string.Empty;
        ErrorMessage = string.Empty;
        LoadPermissionsFromCsv(string.Join(",", AppPermission.DefaultUser));
    }

    [RelayCommand]
    private void StartEditUser(AppUser user)
    {
        IsAddingNew = false;
        IsEditing = true;
        SelectedUser = user;
        EditUsername = user.Username;
        EditDisplayName = user.DisplayName;
        EditRole = user.Role;
        EditIsActive = user.IsActive;
        EditPassword = string.Empty;
        ErrorMessage = string.Empty;
        LoadPermissionsFromCsv(user.Permissions);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        IsAddingNew = false;
        SelectedUser = null;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SaveUserAsync()
    {
        ErrorMessage = string.Empty;

        try
        {
            if (IsAddingNew)
            {
                if (string.IsNullOrWhiteSpace(EditUsername))
                { ErrorMessage = Lang.UsrUsernameRequired; return; }
                if (string.IsNullOrWhiteSpace(EditPassword))
                { ErrorMessage = Lang.UsrPasswordRequired; return; }
                if (string.IsNullOrWhiteSpace(EditDisplayName))
                { ErrorMessage = Lang.UsrDisplayNameRequired; return; }

                await _authService.CreateUserAsync(EditUsername.Trim(), EditPassword, EditDisplayName.Trim(), EditRole, GetPermissionsCsv());

                await LogAuditAsync("CREATE_USER", "User", null,
                    $"Created user '{EditUsername.Trim()}' with role {EditRole}",
                    $"\u0625\u0646\u0634\u0627\u0621 \u0645\u0633\u062a\u062e\u062f\u0645 '{EditUsername.Trim()}' \u0628\u062f\u0648\u0631 {EditRole}");
            }
            else if (SelectedUser != null)
            {
                var wasActive = SelectedUser.IsActive;
                await _authService.UpdateUserAsync(SelectedUser.Id, EditDisplayName.Trim(), EditRole, EditIsActive, GetPermissionsCsv());

                // Log status change if toggled
                if (wasActive != EditIsActive)
                {
                    var action = EditIsActive ? "ACTIVATE_USER" : "DEACTIVATE_USER";
                    var enDetail = EditIsActive
                        ? $"Activated user '{SelectedUser.Username}'"
                        : $"Deactivated user '{SelectedUser.Username}'";
                    var arDetail = EditIsActive
                        ? $"\u062a\u0641\u0639\u064a\u0644 \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645 '{SelectedUser.Username}'"
                        : $"\u062a\u0639\u0637\u064a\u0644 \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645 '{SelectedUser.Username}'";
                    await LogAuditAsync(action, "User", SelectedUser.Id, enDetail, arDetail);
                }
                else
                {
                    await LogAuditAsync("UPDATE_USER", "User", SelectedUser.Id,
                        $"Updated user '{SelectedUser.Username}' (role: {EditRole})",
                        $"\u062a\u062d\u062f\u064a\u062b \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645 '{SelectedUser.Username}' (\u0627\u0644\u062f\u0648\u0631: {EditRole})");
                }

                if (!string.IsNullOrWhiteSpace(EditPassword))
                {
                    await _authService.ResetPasswordAsync(SelectedUser.Id, EditPassword);
                    await LogAuditAsync("RESET_PASSWORD", "User", SelectedUser.Id,
                        $"Reset password for user '{SelectedUser.Username}'",
                        $"\u0625\u0639\u0627\u062f\u0629 \u062a\u0639\u064a\u064a\u0646 \u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631 \u0644\u0644\u0645\u0633\u062a\u062e\u062f\u0645 '{SelectedUser.Username}'");
                }
            }

            IsEditing = false;
            IsAddingNew = false;
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ToggleUserActive(AppUser user)
    {
        if (user == null) return;

        // Prevent deactivating the admin account
        if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase) && user.IsActive)
        {
            CustomMessageBox.Show(
                "Cannot deactivate the default admin account.",
                Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        // Show reason dialog for deactivation, or simple reason dialog for activation
        string? reason = null;
        if (user.IsActive)
        {
            var reasonDialog = new DeleteReasonDialog
            {
                Title = Lang.UsrDeactivateReason,
                Owner = System.Windows.Application.Current.MainWindow
            };
            if (reasonDialog.ShowDialog() != true)
                return;
            reason = reasonDialog.Reason;
        }
        else
        {
            // For activation, use the same reason dialog to confirm
            var reasonDialog = new DeleteReasonDialog
            {
                Title = Lang.UsrActivate,
                Owner = System.Windows.Application.Current.MainWindow
            };
            if (reasonDialog.ShowDialog() != true)
                return;
            reason = reasonDialog.Reason;
        }

        try
        {
            var newStatus = !user.IsActive;
            await _authService.UpdateUserAsync(user.Id, user.DisplayName, user.Role, newStatus, user.Permissions);

            var action = newStatus ? "ACTIVATE_USER" : "DEACTIVATE_USER";
            var reasonSuffix = !string.IsNullOrWhiteSpace(reason) ? $" - Reason: {reason}" : "";
            var reasonSuffixAr = !string.IsNullOrWhiteSpace(reason) ? $" - \u0627\u0644\u0633\u0628\u0628: {reason}" : "";
            var enDetail = newStatus
                ? $"Activated user '{user.Username}'{reasonSuffix}"
                : $"Deactivated user '{user.Username}'{reasonSuffix}";
            var arDetail = newStatus
                ? $"\u062a\u0641\u0639\u064a\u0644 \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645 '{user.Username}'{reasonSuffixAr}"
                : $"\u062a\u0639\u0637\u064a\u0644 \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645 '{user.Username}'{reasonSuffixAr}";

            await LogAuditAsync(action, "User", user.Id, enDetail, arDetail);
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteUser(AppUser user)
    {
        if (user == null) return;

        if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            CustomMessageBox.Show(
                "Cannot delete the default admin account.",
                Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        // Show reason dialog
        var reasonDialog = new DeleteReasonDialog
        {
            Title = Lang.UsrDeleteReason,
            Owner = System.Windows.Application.Current.MainWindow
        };
        if (reasonDialog.ShowDialog() != true)
            return;

        var reason = reasonDialog.Reason;

        try
        {
            await _authService.DeleteUserAsync(user.Id);

            await LogAuditAsync("DELETE_USER", "User", user.Id,
                $"Deleted user '{user.Username}' ({user.DisplayName}) - Reason: {reason}",
                $"\u062d\u0630\u0641 \u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645 '{user.Username}' ({user.DisplayName}) - \u0627\u0644\u0633\u0628\u0628: {reason}");

            // Close edit panel if this user was being edited
            if (SelectedUser?.Id == user.Id)
                CancelEdit();

            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    private async Task LogAuditAsync(string action, string entityType, int? entityId,
        string detailsEn, string detailsAr, string? reason = null)
    {
        try
        {
            var performedBy = _currentUser.DisplayName ?? _currentUser.Username ?? "Unknown";

            // Save to DB via AuditLogRepository
            var auditLog = new AuditLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = detailsEn,
                DetailsAr = detailsAr,
                PerformedBy = performedBy,
                Timestamp = DateTime.UtcNow
            };
            await _auditLogRepository.AddAsync(auditLog);

            // Also log to session file
            await _sessionLogger.LogOperationAsync(action, entityType, entityId,
                detailsEn, detailsAr, performedBy);
        }
        catch
        {
            // Don't let audit logging failures block the operation
        }
    }
}
