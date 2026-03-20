using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AccessControlPro.Application.DTOs;

namespace AccessControlPro.WPF.Views;

public partial class CreateQrPassDialog : Window
{
    public string PlayerName => NameTextBox.Text.Trim();
    public string Phone => PhoneTextBox.Text.Trim();
    public decimal Fee => decimal.TryParse(FeeTextBox.Text.Trim(), out var f) ? f : 0;
    public int ValidDays => int.TryParse(ValidDaysTextBox.Text.Trim(), out var d) && d > 0 ? d : 1;
    public int MaxUses => int.TryParse(MaxUsesTextBox.Text.Trim(), out var m) && m > 0 ? m : 5;

    public int? SelectedDeviceId
    {
        get
        {
            var device = DeviceCombo.SelectedItem as DeviceDto;
            return device?.Id;
        }
    }

    public string SelectedDeviceName
    {
        get
        {
            var device = DeviceCombo.SelectedItem as DeviceDto;
            return device?.Name ?? string.Empty;
        }
    }

    public int SelectedDoorNumber
    {
        get
        {
            var item = DoorCombo.SelectedItem as ComboBoxItem;
            if (item?.Tag is string tag && int.TryParse(tag, out var door))
                return door;
            return 1;
        }
    }

    public CreateQrPassDialog(IEnumerable<DeviceDto> devices)
    {
        InitializeComponent();
        DeviceCombo.ItemsSource = devices;
        if (DeviceCombo.Items.Count > 0)
            DeviceCombo.SelectedIndex = 0;
    }

    private void CreateClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PlayerName))
        {
            CustomMessageBox.Show("Player name is required", "Validation", MsgType.Warning, this);
            NameTextBox.Focus();
            return;
        }

        if (DeviceCombo.SelectedItem == null)
        {
            CustomMessageBox.Show("Please select a device", "Validation", MsgType.Warning, this);
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
