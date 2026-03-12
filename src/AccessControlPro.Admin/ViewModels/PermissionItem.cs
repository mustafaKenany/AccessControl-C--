using CommunityToolkit.Mvvm.ComponentModel;

namespace AccessControlPro.Admin.ViewModels;

public partial class PermissionItem : ObservableObject
{
    public string Key { get; set; } = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _groupName = string.Empty;

    [ObservableProperty]
    private bool _isChecked;
}
