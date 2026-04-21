using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class RenewSubscriptionDialog : Window
{
    private List<SubscriptionPlan> _plans = new();
    private readonly List<DeviceDto> _devices;
    private readonly List<CheckBox> _deviceCheckBoxes = new();
    private readonly ILookupService? _lookupService;

    public LanguageManager Lang => LanguageManager.Instance;

    public string SelectedSubscriptionType { get; private set; } = string.Empty;
    public int SelectedMonths { get; private set; }
    public int SelectedCustomDays { get; private set; }
    public DateTime? CustomStartDateResult { get; private set; }
    public DateTime? CustomEndDateResult { get; private set; }
    public decimal NewFee => decimal.TryParse(FeeTextBox.Text.Trim(), out var f) ? f : 0;
    public decimal NewAmountPaid => decimal.TryParse(PaidTextBox.Text.Trim(), out var p) ? p : 0;

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

    public RenewSubscriptionDialog(EmployeeDto employee, List<DeviceDto> devices, ILookupService? lookupService = null)
    {
        _devices = devices ?? new List<DeviceDto>();
        _lookupService = lookupService;
        InitializeComponent();
        PlayerNameText.Text = $"{employee.FullNameEn} ({employee.CardNo})";
        LoadPlans();
        PopulateSubscriptionTypes();
        PopulatePeriods();
        PopulateEffectiveTimes();
        PopulateDevices();
        SelectSubscriptionType(employee.SubscriptionType);
        UpdateRemaining();

        if (DeviceModeHelper.IsMultiDevice)
        {
            EffectiveTimesLabel.Visibility = Visibility.Collapsed;
            EffectiveTimesCombo.Visibility = Visibility.Collapsed;
        }

        if (_lookupService != null)
            _ = LoadPlansFromDbAsync();
    }

    private void LoadPlans()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubscriptionPlans.json");
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _plans = JsonSerializer.Deserialize<List<SubscriptionPlan>>(json) ?? GetDefaultPlans();
            }
            else
            {
                _plans = GetDefaultPlans();
            }
        }
        catch
        {
            _plans = GetDefaultPlans();
        }
    }

    private static List<SubscriptionPlan> GetDefaultPlans() =>
    [
        new() { Type = "Fitness", MonthlyRate = 25000 },
        new() { Type = "Kickboxing", MonthlyRate = 30000 },
        new() { Type = "Swimming", MonthlyRate = 20000 },
        new() { Type = "CrossFit", MonthlyRate = 35000 },
        new() { Type = "Yoga", MonthlyRate = 15000 },
        new() { Type = "Full Access", MonthlyRate = 50000 }
    ];

    private async Task LoadPlansFromDbAsync()
    {
        try
        {
            var items = await _lookupService!.GetByCategoryAsync("SubscriptionPlan");
            if (items.Count > 0)
            {
                var lang = LanguageManager.Instance;
                _plans = items.Select(i => new SubscriptionPlan
                {
                    Type = i.Name,
                    MonthlyRate = i.NumericValue,
                    DisplayName = lang.IsArabic && !string.IsNullOrWhiteSpace(i.NameAr) ? i.NameAr : i.Name
                }).ToList();
                PopulateSubscriptionTypes();
            }
        }
        catch { }
    }

    private void PopulateSubscriptionTypes()
    {
        SubscriptionTypeCombo.Items.Clear();
        foreach (var plan in _plans)
            SubscriptionTypeCombo.Items.Add(new ComboBoxItem { Content = plan.Label, Tag = plan });
    }

    private void PopulatePeriods()
    {
        PeriodCombo.Items.Clear();
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Lang.Month1, Tag = "1" });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Lang.Months3, Tag = "3" });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Lang.Months6, Tag = "6" });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Lang.Months12, Tag = "12" });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Lang.CustomDays, Tag = "custom" });
    }

    private void PopulateEffectiveTimes()
    {
        EffectiveTimesCombo.Items.Clear();

        // Preset effective times values — must match AssignCardDialog exactly
        var presetValues = new[] { 50, 60, 70, 80, 90, 100, 120, 140, 160, 180, 200, 250, 300, 400, 500 };

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

        // Select Unlimited by default
        EffectiveTimesCombo.SelectedIndex = EffectiveTimesCombo.Items.Count - 1;
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

    private void SelectSubscriptionType(string type)
    {
        for (int i = 0; i < SubscriptionTypeCombo.Items.Count; i++)
        {
            if (SubscriptionTypeCombo.Items[i] is ComboBoxItem item &&
                item.Tag is SubscriptionPlan plan && plan.Type == type)
            {
                SubscriptionTypeCombo.SelectedIndex = i;
                return;
            }
        }
        if (SubscriptionTypeCombo.Items.Count > 0)
            SubscriptionTypeCombo.SelectedIndex = 0;
    }

    private SubscriptionPlan? GetSelectedPlan()
    {
        if (SubscriptionTypeCombo?.SelectedItem is ComboBoxItem item && item.Tag is SubscriptionPlan plan)
            return plan;
        return null;
    }

    private bool IsCustomPeriod()
    {
        return PeriodCombo?.SelectedItem is ComboBoxItem item &&
               item.Tag is string tagStr && tagStr == "custom";
    }

    private int GetSelectedMonths()
    {
        if (PeriodCombo?.SelectedItem is ComboBoxItem item &&
            item.Tag is string tagStr && int.TryParse(tagStr, out int months))
            return months;
        return 0;
    }

    private void SubscriptionTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RecalcFee();
    }

    private void PeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Show/hide custom date range input
        var isCustom = IsCustomPeriod();
        var vis = isCustom ? Visibility.Visible : Visibility.Collapsed;
        if (CustomDaysLabel != null) CustomDaysLabel.Visibility = vis;
        if (CustomDatesPanel != null) CustomDatesPanel.Visibility = vis;

        // Set default dates when switching to custom
        if (isCustom && CustomStartDate != null && CustomEndDate != null)
        {
            if (CustomStartDate.SelectedDate == null)
                CustomStartDate.SelectedDate = DateTime.Today;
            if (CustomEndDate.SelectedDate == null)
                CustomEndDate.SelectedDate = DateTime.Today.AddMonths(1);
        }

        RecalcFee();
    }

    private void CustomDaysTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RecalcFee();
    }

    private void CustomDate_Changed(object sender, SelectionChangedEventArgs e)
    {
        RecalcFee();
    }

    private void RecalcFee()
    {
        if (FeeTextBox == null) return;
        var plan = GetSelectedPlan();

        if (IsCustomPeriod())
        {
            // Calculate fee based on date range
            if (plan != null && CustomStartDate?.SelectedDate != null && CustomEndDate?.SelectedDate != null)
            {
                var days = (CustomEndDate.SelectedDate.Value - CustomStartDate.SelectedDate.Value).Days;
                if (days > 0)
                {
                    var dailyRate = plan.MonthlyRate / 30m;
                    FeeTextBox.Text = Math.Round(dailyRate * days, 0).ToString();
                }
            }
        }
        else
        {
            var months = GetSelectedMonths();
            if (plan != null && months > 0 && plan.MonthlyRate > 0)
                FeeTextBox.Text = (plan.MonthlyRate * months).ToString();
        }

        UpdateRemaining();
    }

    private void FeeTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateRemaining();
    }

    private void PaidTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateRemaining();
    }

    private void UpdateRemaining()
    {
        if (RemainingTextBox == null) return;
        var fee = decimal.TryParse(FeeTextBox.Text.Trim(), out var f) ? f : 0;
        var paid = decimal.TryParse(PaidTextBox.Text.Trim(), out var p) ? p : 0;
        var remaining = fee - paid;
        RemainingTextBox.Text = remaining.ToString();
        RemainingTextBox.Foreground = remaining <= 0
            ? new SolidColorBrush(Color.FromRgb(0x2E, 0xD4, 0x7A))
            : new SolidColorBrush(Color.FromRgb(0xF7, 0x68, 0x5B));
    }

    private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^[0-9]+$");
    }

    private void NumberOnly_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));
            var cleaned = Regex.Replace(text ?? "", @"[^0-9]", "");
            if (string.IsNullOrEmpty(cleaned)) { e.CancelCommand(); return; }
            if (cleaned != text)
            {
                e.CancelCommand();
                if (sender is TextBox tb) tb.SelectedText = cleaned;
            }
        }
        else e.CancelCommand();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var errors = new List<string>();

        if (SubscriptionTypeCombo.SelectedIndex < 0)
            errors.Add(Lang.SubscriptionRequired);

        if (PeriodCombo.SelectedIndex < 0)
            errors.Add(Lang.PeriodRequired);

        if (IsCustomPeriod())
        {
            if (CustomStartDate.SelectedDate == null || CustomEndDate.SelectedDate == null)
                errors.Add("Start and end dates are required");
            else if (CustomEndDate.SelectedDate <= CustomStartDate.SelectedDate)
                errors.Add("End date must be after start date");
        }

        if (string.IsNullOrWhiteSpace(FeeTextBox.Text) || NewFee <= 0)
            errors.Add(Lang.FeeRequired);

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

        var plan = GetSelectedPlan();
        SelectedSubscriptionType = plan?.Type ?? string.Empty;

        if (IsCustomPeriod())
        {
            SelectedMonths = 0;
            CustomStartDateResult = CustomStartDate.SelectedDate;
            CustomEndDateResult = CustomEndDate.SelectedDate;
            SelectedCustomDays = CustomStartDate.SelectedDate != null && CustomEndDate.SelectedDate != null
                ? (CustomEndDate.SelectedDate.Value - CustomStartDate.SelectedDate.Value).Days : 0;
        }
        else
        {
            SelectedMonths = GetSelectedMonths();
            SelectedCustomDays = 0;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
