using System.Windows;
using System.Windows.Controls;
using AccessControlPro.Application.DTOs;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class FreezeReasonDialog : Window
{
    private readonly List<DeviceDto> _devices;
    private readonly List<CheckBox> _deviceCheckBoxes = new();

    public LanguageManager Lang => LanguageManager.Instance;

    public string Reason => ReasonTextBox.Text.Trim();

    public List<int> SelectedDeviceIds
    {
        get
        {
            var ids = new List<int>();
            for (int i = 0; i < _deviceCheckBoxes.Count; i++)
            {
                if (_deviceCheckBoxes[i].IsChecked == true)
                    ids.Add(_devices[i].Id);
            }
            return ids;
        }
    }

    public FreezeReasonDialog(List<DeviceDto>? devices = null)
    {
        _devices = devices ?? new List<DeviceDto>();
        InitializeComponent();
        PopulateDevices();
        ReasonTextBox.Focus();
    }

    private void PopulateDevices()
    {
        _deviceCheckBoxes.Clear();

        if (_devices.Count == 0)
        {
            DevicesSection.Visibility = Visibility.Collapsed;
            return;
        }

        NoDevicesText.Visibility = Visibility.Collapsed;

        foreach (var device in _devices)
        {
            var cb = new CheckBox
            {
                Content = $"{device.Name}  ({device.IP})",
                IsChecked = true,
                Style = (Style)FindResource("DarkCheckBox"),
                Margin = new Thickness(0, 3, 0, 3)
            };
            _deviceCheckBoxes.Add(cb);
            DevicesPanel.Children.Add(cb);
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ReasonTextBox.Text))
        {
            CustomMessageBox.Show(Lang.EnterFreezeReason, Lang.FreezePlayer, MsgType.Warning, this);
            return;
        }

        if (_devices.Count > 0 && SelectedDeviceIds.Count == 0)
        {
            CustomMessageBox.Show(Lang.SelectAtLeastOneDevice, Lang.FreezePlayer, MsgType.Warning, this);
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
