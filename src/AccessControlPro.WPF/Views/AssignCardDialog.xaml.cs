using System.Windows;
using System.Windows.Controls;
using AccessControlPro.Application.DTOs;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class AssignCardDialog : Window
{
    private readonly List<DeviceDto> _devices;
    private readonly List<CheckBox> _deviceCheckBoxes = new();

    public LanguageManager Lang => LanguageManager.Instance;

    public string CardNumber => CardNumberTextBox.Text.Trim();
    public string CardPassword => "";
    public int OpenMode => 0; // Always Ordinary
    public string CardType => "Standard";

    public string DoorPermissions
    {
        get
        {
            var d1 = Door1Check.IsChecked == true ? "01" : "00";
            var d2 = Door2Check.IsChecked == true ? "01" : "00";
            var d3 = Door3Check.IsChecked == true ? "01" : "00";
            var d4 = Door4Check.IsChecked == true ? "01" : "00";
            return $"{d1}{d2}{d3}{d4}";
        }
    }

    public int EffectiveTimes
    {
        get
        {
            if (EffectiveTimesCombo.SelectedItem is ComboBoxItem item && item.Tag is int val)
                return val;
            return 65535;
        }
    }

    public int TimePeriodIndex => 1;
    public bool HolidayEnabled => HolidayCheck.IsChecked == true;
    public DateTime ValidFrom { get; private set; }
    public DateTime ValidTo { get; private set; }

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

    public AssignCardDialog(EmployeeDto employee, List<DeviceDto> devices)
    {
        _devices = devices ?? new List<DeviceDto>();
        InitializeComponent();

        // Auto-fill from player
        CardNumberTextBox.Text = employee.CardNo;
        ValidFrom = employee.StartDate;
        ValidTo = employee.EndDate;
        ValidFromText.Text = employee.StartDate.ToString("yyyy-MM-dd");
        ValidToText.Text = employee.EndDate.ToString("yyyy-MM-dd");

        PopulateEffectiveTimes();
        PopulateDevices();
    }

    private void PopulateEffectiveTimes()
    {
        EffectiveTimesCombo.Items.Clear();
        EffectiveTimesCombo.Items.Add(new ComboBoxItem { Content = $"{Lang.Unlimited} (65535)", Tag = 65535 });
        EffectiveTimesCombo.Items.Add(new ComboBoxItem { Content = $"{Lang.InvalidateImmediately} (0)", Tag = 0 });
        for (int i = 1; i <= 1000; i++)
            EffectiveTimesCombo.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = i });
        EffectiveTimesCombo.SelectedIndex = 0;
    }

    private void PopulateDevices()
    {
        _deviceCheckBoxes.Clear();

        if (_devices.Count == 0)
        {
            NoDevicesText.Visibility = Visibility.Visible;
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
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(CardNumber))
            errors.Add(Lang.CardNumberRequired);

        if (Door1Check.IsChecked != true && Door2Check.IsChecked != true &&
            Door3Check.IsChecked != true && Door4Check.IsChecked != true)
            errors.Add(Lang.SelectAtLeastOneDoor);

        if (_devices.Count > 0 && SelectedDeviceIds.Count == 0)
            errors.Add(Lang.SelectAtLeastOneDevice);

        if (errors.Count > 0)
        {
            var message = string.Join("\n", errors.Select(err => $"  \u2022  {err}"));
            CustomMessageBox.Show(message, Lang.ValidationTitle, MsgType.Warning, this);
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
