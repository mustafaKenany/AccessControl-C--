using System.Collections.ObjectModel;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.Admin.ViewModels;

public partial class UsersViewModel : ObservableObject
{
    private readonly IAuthService _authService;

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

    public ObservableCollection<AppUser> Users { get; } = new();
    public ObservableCollection<PermissionItem> EditPermissions { get; } = new();

    public string[] AvailableRoles { get; } = ["Admin", "User"];

    public UsersViewModel(IAuthService authService)
    {
        _authService = authService;
        InitializePermissionItems();

        // Re-initialize permission display names when language switches
        Lang.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Lang.PermPermissions))
                RefreshPermissionDisplayNames();
        };
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
            }
            else if (SelectedUser != null)
            {
                await _authService.UpdateUserAsync(SelectedUser.Id, EditDisplayName.Trim(), EditRole, EditIsActive, GetPermissionsCsv());

                if (!string.IsNullOrWhiteSpace(EditPassword))
                    await _authService.ResetPasswordAsync(SelectedUser.Id, EditPassword);
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
}
