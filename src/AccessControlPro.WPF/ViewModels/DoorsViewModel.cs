using System.Collections.ObjectModel;
using System.Windows;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

public partial class DoorsViewModel : ObservableObject
{
    private readonly IDoorService _doorService;
    private List<DoorDto> _allDoors = new();

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _openCount;

    [ObservableProperty]
    private int _closedCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<DoorDto> Doors { get; } = new();

    public DoorsViewModel(IDoorService doorService)
    {
        _doorService = doorService;
        _ = LoadDoorsAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDoorsAsync();
    }

    [RelayCommand]
    private async Task OpenDoorAsync(DoorDto? door)
    {
        if (door == null) return;
        try
        {
            StatusMessage = $"Opening {door.Name}...";
            var success = await _doorService.OpenDoorAsync(door.Id);
            if (success)
            {
                StatusMessage = Lang.OpenDoorSuccess;
                CustomMessageBox.Show($"{Lang.OpenDoorSuccess}\n{door.Name}", Lang.OpenDoor,
                    MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = "Failed to open door";
                CustomMessageBox.Show("Failed to open door.", Lang.OpenDoor,
                    MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to open door";
            CustomMessageBox.Show($"Failed to open door: {ex.Message}", Lang.OpenDoor,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task CloseDoorAsync(DoorDto? door)
    {
        if (door == null) return;
        try
        {
            StatusMessage = $"Closing {door.Name}...";
            var success = await _doorService.CloseDoorAsync(door.Id);
            if (success)
            {
                StatusMessage = Lang.CloseDoorSuccess;
                CustomMessageBox.Show($"{Lang.CloseDoorSuccess}\n{door.Name}", Lang.CloseDoor,
                    MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = "Failed to close door";
                CustomMessageBox.Show("Failed to close door.", Lang.CloseDoor,
                    MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to close door";
            CustomMessageBox.Show($"Failed to close door: {ex.Message}", Lang.CloseDoor,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task SetDelayAsync(DoorDto? door)
    {
        if (door == null) return;
        try
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                Lang.EnterDelay, Lang.SetDelay, "5");

            if (string.IsNullOrWhiteSpace(input)) return;
            if (!int.TryParse(input, out var seconds) || seconds < 1 || seconds > 99)
            {
                CustomMessageBox.Show("Invalid value. Enter 1-99 seconds.",
                    Lang.SetDelay, MsgType.Warning, System.Windows.Application.Current.MainWindow);
                return;
            }

            StatusMessage = $"Setting delay for {door.Name}...";
            var success = await _doorService.SetDoorDelayAsync(door.Id, seconds);
            if (success)
            {
                StatusMessage = Lang.SetDelaySuccess;
                CustomMessageBox.Show($"{Lang.SetDelaySuccess}\n{door.Name}: {seconds}s", Lang.SetDelay,
                    MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = "Failed to set delay";
                CustomMessageBox.Show("Failed to set door delay.", Lang.SetDelay,
                    MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to set delay";
            CustomMessageBox.Show($"Failed to set delay: {ex.Message}", Lang.SetDelay,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task SetPasswordAsync(DoorDto? door)
    {
        if (door == null) return;
        try
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                Lang.EnterPassword, Lang.SetPassword, "");

            if (string.IsNullOrWhiteSpace(input)) return;

            StatusMessage = $"Setting password for {door.Name}...";
            var success = await _doorService.SetDoorPasswordAsync(door.Id, input);
            if (success)
            {
                StatusMessage = Lang.SetPasswordSuccess;
                CustomMessageBox.Show($"{Lang.SetPasswordSuccess}\n{door.Name}", Lang.SetPassword,
                    MsgType.Success, System.Windows.Application.Current.MainWindow);
            }
            else
            {
                StatusMessage = "Failed to set password";
                CustomMessageBox.Show("Failed to set door password.", Lang.SetPassword,
                    MsgType.Warning, System.Windows.Application.Current.MainWindow);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to set password";
            CustomMessageBox.Show($"Failed to set password: {ex.Message}", Lang.SetPassword,
                MsgType.Error, System.Windows.Application.Current.MainWindow);
        }
    }

    [RelayCommand]
    private async Task RenameDoorAsync(DoorDto? door)
    {
        if (door == null) return;
        var input = Microsoft.VisualBasic.Interaction.InputBox(
            Lang.EnterName, Lang.Edit, door.Name);

        if (string.IsNullOrWhiteSpace(input) || input == door.Name) return;

        var success = await _doorService.RenameDoorAsync(door.Id, input);
        if (success)
        {
            await LoadDoorsAsync();
            CustomMessageBox.Show($"{Lang.RenameSuccess}\n{input}", Lang.Edit,
                MsgType.Success, System.Windows.Application.Current.MainWindow);
        }
    }

    private async Task LoadDoorsAsync()
    {
        IsLoading = true;
        try
        {
            var doors = await _doorService.GetAllDoorsAsync();
            _allDoors = doors.ToList();

            TotalCount = _allDoors.Count;
            OpenCount = _allDoors.Count(d => d.Status == "Open");
            ClosedCount = _allDoors.Count(d => d.Status == "Closed");

            ApplyFilter();
        }
        catch
        {
            // Empty state on first run
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        Doors.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allDoors
            : _allDoors.Where(d =>
                d.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceIP.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        foreach (var door in filtered)
            Doors.Add(door);
    }
}
