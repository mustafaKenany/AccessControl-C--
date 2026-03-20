using System.Windows;
using System.Windows.Controls;
using AccessControlPro.Application.DTOs;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class SetDoorScheduleDialog : Window
{
    public LanguageManager Lang => LanguageManager.Instance;

    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }
    public bool Is24Hours { get; private set; } = true;
    public string WorkingDaysResult { get; private set; } = "1,2,3,4,5,6,7";

    private readonly CheckBox[] _dayCheckBoxes;

    public SetDoorScheduleDialog(DoorDto door)
    {
        InitializeComponent();
        DataContext = this;

        DoorNameText.Text = door.Name;

        // Populate hour/minute combos
        for (int h = 0; h < 24; h++)
        {
            StartHourCombo.Items.Add(new ComboBoxItem { Content = h.ToString("D2") });
            EndHourCombo.Items.Add(new ComboBoxItem { Content = h.ToString("D2") });
        }
        foreach (var m in new[] { 0, 15, 30, 45 })
        {
            StartMinuteCombo.Items.Add(new ComboBoxItem { Content = m.ToString("D2") });
            EndMinuteCombo.Items.Add(new ComboBoxItem { Content = m.ToString("D2") });
        }

        _dayCheckBoxes = new[] { DayMon, DayTue, DayWed, DayThu, DayFri, DaySat, DaySun };

        // Set day labels (bilingual)
        DayMon.Content = Lang.Monday;
        DayTue.Content = Lang.Tuesday;
        DayWed.Content = Lang.Wednesday;
        DayThu.Content = Lang.Thursday;
        DayFri.Content = Lang.Friday;
        DaySat.Content = Lang.Saturday;
        DaySun.Content = Lang.Sunday;

        // Load existing values
        Is24HoursCheckBox.IsChecked = door.Is24Hours;
        SelectComboByContent(StartHourCombo, door.WorkStartTime.Hours.ToString("D2"));
        SelectComboByContent(StartMinuteCombo, RoundToNearest15(door.WorkStartTime.Minutes).ToString("D2"));
        SelectComboByContent(EndHourCombo, door.WorkEndTime.Hours.ToString("D2"));
        SelectComboByContent(EndMinuteCombo, RoundToNearest15(door.WorkEndTime.Minutes).ToString("D2"));

        // Set default selections if nothing matched
        if (StartHourCombo.SelectedIndex < 0) StartHourCombo.SelectedIndex = 0;
        if (StartMinuteCombo.SelectedIndex < 0) StartMinuteCombo.SelectedIndex = 0;
        if (EndHourCombo.SelectedIndex < 0) EndHourCombo.SelectedIndex = 23;
        if (EndMinuteCombo.SelectedIndex < 0) EndMinuteCombo.SelectedIndex = 3; // 45

        // Parse working days
        var activeDays = door.WorkingDays.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var d) ? d : 0)
            .ToHashSet();
        for (int i = 0; i < 7; i++)
            _dayCheckBoxes[i].IsChecked = activeDays.Contains(i + 1);

        UpdateTimePanelVisibility();
    }

    private static int RoundToNearest15(int minutes) => (minutes / 15) * 15;

    private static void SelectComboByContent(ComboBox combo, string value)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Content?.ToString() == value)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    private void Is24HoursCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateTimePanelVisibility();
    }

    private void UpdateTimePanelVisibility()
    {
        if (TimePanel == null) return;
        TimePanel.Visibility = Is24HoursCheckBox.IsChecked == true
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Is24Hours = Is24HoursCheckBox.IsChecked == true;

        if (Is24Hours)
        {
            StartTime = TimeSpan.Zero;
            EndTime = new TimeSpan(23, 59, 59);
        }
        else
        {
            int startH = GetComboValue(StartHourCombo, 0);
            int startM = GetComboValue(StartMinuteCombo, 0);
            int endH = GetComboValue(EndHourCombo, 23);
            int endM = GetComboValue(EndMinuteCombo, 59);
            StartTime = new TimeSpan(startH, startM, 0);
            EndTime = new TimeSpan(endH, endM, 59);
        }

        // Build working days string
        var days = new List<string>();
        for (int i = 0; i < 7; i++)
        {
            if (_dayCheckBoxes[i].IsChecked == true)
                days.Add((i + 1).ToString());
        }
        WorkingDaysResult = days.Count > 0 ? string.Join(",", days) : "1,2,3,4,5,6,7";

        DialogResult = true;
    }

    private static int GetComboValue(ComboBox combo, int defaultValue)
    {
        if (combo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Content?.ToString(), out var val))
            return val;
        return defaultValue;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
