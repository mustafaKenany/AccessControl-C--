using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AccessControlPro.Application.DTOs;
using AccessControlPro.WPF.Helpers;
using FontAwesome.WPF;

namespace AccessControlPro.WPF.Views;

public partial class SetDoorScheduleDialog : Window
{
    public LanguageManager Lang => LanguageManager.Instance;

    // Backward-compatible properties
    public TimeSpan StartTime { get; private set; }
    public TimeSpan EndTime { get; private set; }
    public bool Is24Hours { get; private set; } = true;
    public string WorkingDaysResult { get; private set; } = "1,2,3,4,5,6,7";

    // New: full schedule as JSON
    public string ScheduleJson { get; private set; } = string.Empty;

    private readonly List<DaySchedule> _days;
    private const int MaxSegmentsPerDay = 3;

    public SetDoorScheduleDialog(DoorDto door)
    {
        InitializeComponent();
        DataContext = this;

        DoorNameText.Text = door.Name;

        // Iraq week order: Sat, Sun, Mon, Tue, Wed, Thu, Fri
        // Day numbers: Mon=1, Tue=2, Wed=3, Thu=4, Fri=5, Sat=6, Sun=7
        _days = new List<DaySchedule>
        {
            new("Sat", Lang.Saturday, "\u0627\u0644\u0633\u0628\u062A", 6),
            new("Sun", Lang.Sunday, "\u0627\u0644\u0623\u062D\u062F", 7),
            new("Mon", Lang.Monday, "\u0627\u0644\u0627\u062B\u0646\u064A\u0646", 1),
            new("Tue", Lang.Tuesday, "\u0627\u0644\u062B\u0644\u0627\u062B\u0627\u0621", 2),
            new("Wed", Lang.Wednesday, "\u0627\u0644\u0623\u0631\u0628\u0639\u0627\u0621", 3),
            new("Thu", Lang.Thursday, "\u0627\u0644\u062E\u0645\u064A\u0633", 4),
            new("Fri", Lang.Friday, "\u0627\u0644\u062C\u0645\u0639\u0629", 5),
        };

        // Parse existing working days
        var activeDays = door.WorkingDays
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var d) ? d : 0)
            .ToHashSet();

        // Load existing schedule into each day
        foreach (var day in _days)
        {
            day.IsEnabled = activeDays.Contains(day.DayNumber);

            if (day.IsEnabled)
            {
                if (door.Is24Hours)
                {
                    day.Segments.Add(new TimeSegment(0, 0, 23, 59));
                }
                else
                {
                    day.Segments.Add(new TimeSegment(
                        door.WorkStartTime.Hours,
                        RoundToNearest15(door.WorkStartTime.Minutes),
                        door.WorkEndTime.Hours,
                        RoundToNearest15(door.WorkEndTime.Minutes)));
                }
            }
            else
            {
                // Disabled days get a default segment (hidden)
                day.Segments.Add(new TimeSegment(0, 0, 23, 59));
            }
        }

        BuildScheduleUI();
    }

    private static int RoundToNearest15(int minutes)
    {
        int rounded = (minutes / 15) * 15;
        return rounded > 45 ? 45 : rounded;
    }

    #region UI Building

    private void BuildScheduleUI()
    {
        DaysContainer.Children.Clear();

        foreach (var day in _days)
        {
            var dayPanel = CreateDayPanel(day);
            DaysContainer.Children.Add(dayPanel);
        }
    }

    private Border CreateDayPanel(DaySchedule day)
    {
        var outerBorder = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#081B1D")),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(14, 10, 14, 10),
            Tag = day,
        };

        var mainStack = new StackPanel();

        // Header row: checkbox + day name + add button
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Checkbox
        var enableCb = new CheckBox
        {
            IsChecked = day.IsEnabled,
            Style = (Style)FindResource("DayEnableCheckBox"),
            Margin = new Thickness(0, 0, 10, 0),
            Tag = day,
        };
        enableCb.Checked += DayEnable_Changed;
        enableCb.Unchecked += DayEnable_Changed;
        Grid.SetColumn(enableCb, 0);
        headerGrid.Children.Add(enableCb);

        // Day name (bilingual)
        var isArabic = Lang.FlowDirection == FlowDirection.RightToLeft;
        var dayLabel = new TextBlock
        {
            Text = isArabic ? $"{day.DayNameAr}" : $"{day.DayNameEn} / {day.DayNameAr}",
            FontFamily = new FontFamily("Cairo"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(day.IsEnabled ? "#E8F1F2" : "#5A7A7C")),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = day,
        };
        Grid.SetColumn(dayLabel, 1);
        headerGrid.Children.Add(dayLabel);

        // Closed label (shown when disabled)
        var closedLabel = new TextBlock
        {
            Text = Lang.TgClosed,
            FontFamily = new FontFamily("Cairo"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6A4040")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            Visibility = day.IsEnabled ? Visibility.Collapsed : Visibility.Visible,
            Tag = "closedLabel",
        };
        Grid.SetColumn(closedLabel, 1);
        headerGrid.Children.Add(closedLabel);

        // Add segment button
        var addBtn = new Button
        {
            Style = (Style)FindResource("AddSegmentButton"),
            Visibility = day.IsEnabled ? Visibility.Visible : Visibility.Collapsed,
            Tag = day,
        };
        var addBtnContent = new TextBlock
        {
            Text = Lang.TgAddSegment,
            FontFamily = new FontFamily("Cairo"),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#44A1A0")),
        };
        addBtn.Content = addBtnContent;
        addBtn.Click += AddSegment_Click;
        Grid.SetColumn(addBtn, 2);
        headerGrid.Children.Add(addBtn);

        mainStack.Children.Add(headerGrid);

        // Segments container
        var segmentsPanel = new StackPanel
        {
            Margin = new Thickness(30, 6, 0, 0),
            Visibility = day.IsEnabled ? Visibility.Visible : Visibility.Collapsed,
            Tag = "segmentsPanel",
        };

        foreach (var segment in day.Segments)
        {
            segmentsPanel.Children.Add(CreateSegmentRow(day, segment));
        }

        mainStack.Children.Add(segmentsPanel);
        outerBorder.Child = mainStack;

        return outerBorder;
    }

    private Border CreateSegmentRow(DaySchedule day, TimeSegment segment)
    {
        var rowBorder = new Border
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0E2E30")),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 4),
            Padding = new Thickness(10, 6, 10, 6),
            Tag = segment,
        };

        var rowGrid = new Grid();
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Start hour
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // :
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Start min
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // separator
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // End hour
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // :
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // End min
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // spacer
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // delete btn

        // Start Hour
        var startHourCombo = CreateTimeCombo(24, segment.StartHour);
        startHourCombo.Tag = new SegmentComboTag(segment, "StartHour");
        startHourCombo.SelectionChanged += TimeCombo_Changed;
        Grid.SetColumn(startHourCombo, 0);
        rowGrid.Children.Add(startHourCombo);

        // :
        var colon1 = CreateColonText();
        Grid.SetColumn(colon1, 1);
        rowGrid.Children.Add(colon1);

        // Start Minute
        var startMinCombo = CreateMinuteCombo(segment.StartMinute);
        startMinCombo.Tag = new SegmentComboTag(segment, "StartMinute");
        startMinCombo.SelectionChanged += TimeCombo_Changed;
        Grid.SetColumn(startMinCombo, 2);
        rowGrid.Children.Add(startMinCombo);

        // Separator arrow
        var separator = new TextBlock
        {
            Text = "\u2500\u2500",
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#44A1A0")),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 10, 0),
        };
        Grid.SetColumn(separator, 3);
        rowGrid.Children.Add(separator);

        // End Hour
        var endHourCombo = CreateTimeCombo(24, segment.EndHour);
        endHourCombo.Tag = new SegmentComboTag(segment, "EndHour");
        endHourCombo.SelectionChanged += TimeCombo_Changed;
        Grid.SetColumn(endHourCombo, 4);
        rowGrid.Children.Add(endHourCombo);

        // :
        var colon2 = CreateColonText();
        Grid.SetColumn(colon2, 5);
        rowGrid.Children.Add(colon2);

        // End Minute
        var endMinCombo = CreateMinuteCombo(segment.EndMinute);
        endMinCombo.Tag = new SegmentComboTag(segment, "EndMinute");
        endMinCombo.SelectionChanged += TimeCombo_Changed;
        Grid.SetColumn(endMinCombo, 6);
        rowGrid.Children.Add(endMinCombo);

        // Delete button
        var deleteBtn = new Button
        {
            Style = (Style)FindResource("DeleteSegmentButton"),
            Tag = new DeleteSegmentTag(day, segment),
        };
        var trashIcon = new ImageAwesome
        {
            Icon = FontAwesomeIcon.Trash,
            Width = 14,
            Height = 14,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C04040")),
        };
        deleteBtn.Content = trashIcon;
        deleteBtn.Click += DeleteSegment_Click;
        Grid.SetColumn(deleteBtn, 8);
        rowGrid.Children.Add(deleteBtn);

        rowBorder.Child = rowGrid;
        return rowBorder;
    }

    private ComboBox CreateTimeCombo(int count, int selectedValue)
    {
        var combo = new ComboBox
        {
            Style = (Style)FindResource("TimeComboBox"),
        };
        for (int i = 0; i < count; i++)
        {
            combo.Items.Add(new ComboBoxItem { Content = i.ToString("D2") });
        }
        if (selectedValue >= 0 && selectedValue < count)
            combo.SelectedIndex = selectedValue;
        else
            combo.SelectedIndex = 0;
        return combo;
    }

    private ComboBox CreateMinuteCombo(int selectedValue)
    {
        var combo = new ComboBox
        {
            Style = (Style)FindResource("TimeComboBox"),
        };
        var minutes = new[] { 0, 15, 30, 45, 59 };
        foreach (var m in minutes)
        {
            combo.Items.Add(new ComboBoxItem { Content = m.ToString("D2") });
        }

        // Find closest match
        int bestIdx = 0;
        int bestDiff = int.MaxValue;
        for (int i = 0; i < minutes.Length; i++)
        {
            int diff = Math.Abs(minutes[i] - selectedValue);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestIdx = i;
            }
        }
        combo.SelectedIndex = bestIdx;
        return combo;
    }

    private static TextBlock CreateColonText()
    {
        return new TextBlock
        {
            Text = ":",
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#44A1A0")),
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0),
        };
    }

    #endregion

    #region Event Handlers

    private void DayEnable_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not DaySchedule day) return;
        day.IsEnabled = cb.IsChecked == true;
        BuildScheduleUI();
    }

    private void AddSegment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DaySchedule day) return;
        if (day.Segments.Count >= MaxSegmentsPerDay) return;

        day.Segments.Add(new TimeSegment(0, 0, 23, 59));
        BuildScheduleUI();
    }

    private void DeleteSegment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DeleteSegmentTag tag) return;

        // Don't allow deleting the last segment - just clear it
        if (tag.Day.Segments.Count <= 1) return;

        tag.Day.Segments.Remove(tag.Segment);
        BuildScheduleUI();
    }

    private void TimeCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.Tag is not SegmentComboTag tag) return;
        if (combo.SelectedItem is not ComboBoxItem item) return;
        if (!int.TryParse(item.Content?.ToString(), out var val)) return;

        switch (tag.PropertyName)
        {
            case "StartHour": tag.Segment.StartHour = val; break;
            case "StartMinute": tag.Segment.StartMinute = val; break;
            case "EndHour": tag.Segment.EndHour = val; break;
            case "EndMinute": tag.Segment.EndMinute = val; break;
        }
    }

    #endregion

    #region Presets

    private void Preset247_Click(object sender, RoutedEventArgs e)
    {
        foreach (var day in _days)
        {
            day.IsEnabled = true;
            day.Segments.Clear();
            day.Segments.Add(new TimeSegment(0, 0, 23, 59));
        }
        BuildScheduleUI();
    }

    private void PresetWeekdays_Click(object sender, RoutedEventArgs e)
    {
        // Weekdays: Sat-Thu 06:00-23:00, Fri closed (Iraq standard)
        foreach (var day in _days)
        {
            if (day.DayKey == "Fri")
            {
                day.IsEnabled = false;
                day.Segments.Clear();
                day.Segments.Add(new TimeSegment(0, 0, 23, 59));
            }
            else
            {
                day.IsEnabled = true;
                day.Segments.Clear();
                day.Segments.Add(new TimeSegment(6, 0, 23, 0));
            }
        }
        BuildScheduleUI();
    }

    private void PresetCustom_Click(object sender, RoutedEventArgs e)
    {
        foreach (var day in _days)
        {
            day.IsEnabled = false;
            day.Segments.Clear();
            day.Segments.Add(new TimeSegment(0, 0, 23, 59));
        }
        BuildScheduleUI();
    }

    #endregion

    #region Save / Cancel

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Build ScheduleJson
        var scheduleDict = new Dictionary<string, List<string>>();
        foreach (var day in _days)
        {
            var slots = new List<string>();
            if (day.IsEnabled)
            {
                foreach (var seg in day.Segments)
                {
                    slots.Add($"{seg.StartHour:D2}:{seg.StartMinute:D2}-{seg.EndHour:D2}:{seg.EndMinute:D2}");
                }
            }
            scheduleDict[day.DayKey] = slots;
        }
        ScheduleJson = JsonSerializer.Serialize(scheduleDict);

        // Build WorkingDaysResult (backward compat)
        var enabledDayNumbers = _days
            .Where(d => d.IsEnabled)
            .Select(d => d.DayNumber.ToString())
            .ToList();
        WorkingDaysResult = enabledDayNumbers.Count > 0
            ? string.Join(",", enabledDayNumbers)
            : "1,2,3,4,5,6,7";

        // Determine Is24Hours: all enabled days have single segment 00:00-23:59
        var enabledDays = _days.Where(d => d.IsEnabled).ToList();
        Is24Hours = enabledDays.Count == 7 && enabledDays.All(d =>
            d.Segments.Count == 1 &&
            d.Segments[0].StartHour == 0 && d.Segments[0].StartMinute == 0 &&
            d.Segments[0].EndHour == 23 && d.Segments[0].EndMinute == 59);

        // Backward compat: StartTime/EndTime from first enabled day's first segment
        var firstEnabled = _days.FirstOrDefault(d => d.IsEnabled && d.Segments.Count > 0);
        if (firstEnabled != null)
        {
            var seg = firstEnabled.Segments[0];
            StartTime = new TimeSpan(seg.StartHour, seg.StartMinute, 0);
            EndTime = new TimeSpan(seg.EndHour, seg.EndMinute, 59);
        }
        else
        {
            StartTime = TimeSpan.Zero;
            EndTime = new TimeSpan(23, 59, 59);
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    #endregion

    #region Internal Models

    private class DaySchedule
    {
        public string DayKey { get; set; }
        public string DayNameEn { get; set; }
        public string DayNameAr { get; set; }
        public int DayNumber { get; set; } // 1=Mon,...,7=Sun
        public bool IsEnabled { get; set; } = true;
        public List<TimeSegment> Segments { get; set; } = new();

        public DaySchedule(string key, string nameEn, string nameAr, int dayNumber)
        {
            DayKey = key;
            DayNameEn = nameEn;
            DayNameAr = nameAr;
            DayNumber = dayNumber;
        }
    }

    private class TimeSegment
    {
        public int StartHour { get; set; }
        public int StartMinute { get; set; }
        public int EndHour { get; set; }
        public int EndMinute { get; set; }

        public TimeSegment(int sh, int sm, int eh, int em)
        {
            StartHour = sh;
            StartMinute = sm;
            EndHour = eh;
            EndMinute = em;
        }
    }

    private record SegmentComboTag(TimeSegment Segment, string PropertyName);
    private record DeleteSegmentTag(DaySchedule Day, TimeSegment Segment);

    #endregion
}
