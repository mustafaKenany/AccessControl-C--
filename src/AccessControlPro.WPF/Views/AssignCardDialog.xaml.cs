using System.Windows;
using System.Windows.Controls;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class AssignCardDialog : Window
{
    private readonly List<DeviceDto> _devices;
    private readonly List<CheckBox> _deviceCheckBoxes = new();
    private List<TimeGroup> _timeGroups = new();

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

    private int _effectiveTimes = 65535;
    public int EffectiveTimes => _effectiveTimes;

    private int _timePeriodIndex = 1;
    public int TimePeriodIndex => _timePeriodIndex;
    public bool HolidayEnabled => false;
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

    public AssignCardDialog(EmployeeDto employee, List<DeviceDto> devices, ITimeGroupService? timeGroupService = null)
    {
        _devices = devices ?? new List<DeviceDto>();
        InitializeComponent();

        // Auto-fill from player
        CardNumberTextBox.Text = employee.CardNo;
        ValidFrom = employee.StartDate;
        ValidTo = employee.EndDate;
        ValidFromText.Text = employee.StartDate.ToString("yyyy-MM-dd");
        ValidToText.Text = employee.EndDate.ToString("yyyy-MM-dd");

        PopulateEffectiveTimes(employee.MaxVisits);
        PopulateDevices();

        // Load TimeGroups synchronously to ensure combo is populated before dialog shows
        PopulateTimeGroupsSync(timeGroupService);

        if (DeviceModeHelper.IsMultiDevice)
        {
            EffectiveTimesLabel.Visibility = Visibility.Collapsed;
            EffectiveTimesCombo.Visibility = Visibility.Collapsed;
        }
    }

    private void PopulateTimeGroupsSync(ITimeGroupService? timeGroupService)
    {
        try
        {
            if (timeGroupService != null)
            {
                var groups = Task.Run(() => timeGroupService.GetAllAsync()).GetAwaiter().GetResult().ToList();
                _timeGroups = groups;

                TimeGroupCombo.Items.Clear();
                foreach (var g in groups)
                {
                    var displayName = Lang.IsArabic && !string.IsNullOrWhiteSpace(g.NameAr)
                        ? g.NameAr : g.NameEn;
                    TimeGroupCombo.Items.Add(new ComboBoxItem
                    {
                        Content = $"{displayName}  (#{g.HardwareIndex})",
                        Tag = g.HardwareIndex
                    });
                }

                if (TimeGroupCombo.Items.Count > 0)
                    TimeGroupCombo.SelectedIndex = 0;
            }
        }
        catch { }

        // Ensure at least default option exists
        if (TimeGroupCombo.Items.Count == 0)
        {
            TimeGroupCombo.Items.Add(new ComboBoxItem
            {
                Content = "24/7 Full Access (#1)",
                Tag = 1
            });
            TimeGroupCombo.SelectedIndex = 0;
        }

        TimeGroupCombo.SelectionChanged += TimeGroupCombo_SelectionChanged;
        UpdateTimePeriodIndex();
    }

    // TimeGroups are now loaded synchronously in PopulateTimeGroupsSync() to ensure
    // the combo is populated before the dialog is shown to the user.

    private void TimeGroupCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateTimePeriodIndex();
    }

    private void UpdateTimePeriodIndex()
    {
        if (TimeGroupCombo.SelectedItem is ComboBoxItem selected && selected.Tag is int idx)
            _timePeriodIndex = idx;
        else
            _timePeriodIndex = 1;
    }

    private void PopulateEffectiveTimes(int maxVisits)
    {
        EffectiveTimesCombo.Items.Clear();

        // Preset effective times values
        var presetValues = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 120, 140, 160, 180, 200, 250, 300, 400, 500 };

        foreach (var val in presetValues)
        {
            EffectiveTimesCombo.Items.Add(new ComboBoxItem
            {
                Content = val.ToString(),
                Tag = val
            });
        }

        // Unlimited option
        EffectiveTimesCombo.Items.Add(new ComboBoxItem
        {
            Content = $"{Lang.Unlimited} (65535)",
            Tag = 65535
        });

        // Select current value if maxVisits matches a preset, otherwise select Unlimited
        if (maxVisits > 0)
        {
            var effectiveTimesValue = maxVisits;
            bool found = false;
            for (int i = 0; i < EffectiveTimesCombo.Items.Count; i++)
            {
                if (EffectiveTimesCombo.Items[i] is ComboBoxItem item && item.Tag is int tag && tag == effectiveTimesValue)
                {
                    EffectiveTimesCombo.SelectedIndex = i;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                // Add the current value as a custom option and select it
                var customItem = new ComboBoxItem { Content = effectiveTimesValue.ToString(), Tag = effectiveTimesValue };
                EffectiveTimesCombo.Items.Insert(0, customItem);
                EffectiveTimesCombo.SelectedIndex = 0;
            }
        }
        else
        {
            // Select Unlimited by default
            EffectiveTimesCombo.SelectedIndex = EffectiveTimesCombo.Items.Count - 1;
        }

        EffectiveTimesCombo.SelectionChanged += EffectiveTimesCombo_SelectionChanged;
        UpdateEffectiveTimes();
        EffectiveTimesCombo.IsEnabled = true;
    }

    private void EffectiveTimesCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateEffectiveTimes();
    }

    private void UpdateEffectiveTimes()
    {
        if (EffectiveTimesCombo.SelectedItem is ComboBoxItem selected && selected.Tag is int val)
            _effectiveTimes = val;
        else
            _effectiveTimes = 65535;
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
