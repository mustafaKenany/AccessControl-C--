using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AccessControlPro.Application.DTOs;

namespace AccessControlPro.WPF.Views;

public partial class CreateQrPassDialog : Window
{
    private readonly List<DeviceDto> _devices;

    public string PlayerName => NameTextBox.Text.Trim();
    public string Phone => PhoneTextBox.Text.Trim();
    public decimal Fee => decimal.TryParse(FeeTextBox.Text.Trim(), out var f) ? f : 5000;

    // Hardcoded defaults
    public int ValidDays => 365;       // 1 year
    public int MaxUses => 2;           // enter + exit
    public string DoorPermissions => "01010000"; // all doors

    // Auto-select first device (all devices will be synced)
    public int? SelectedDeviceId => _devices.FirstOrDefault()?.Id;
    public string SelectedDeviceName => _devices.FirstOrDefault()?.Name ?? string.Empty;
    public int SelectedDoorNumber => 1;

    public IReadOnlyList<DeviceDto> AllDevices => _devices;

    public CreateQrPassDialog(IEnumerable<DeviceDto> devices)
    {
        _devices = devices?.ToList() ?? new List<DeviceDto>();
        InitializeComponent();
        FeeTextBox.Text = "5000";
    }

    private void CreateClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PlayerName))
        {
            CustomMessageBox.Show("Player name is required", "Validation", MsgType.Warning, this);
            NameTextBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
