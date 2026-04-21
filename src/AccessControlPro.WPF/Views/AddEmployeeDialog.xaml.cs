using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using Microsoft.Win32;

namespace AccessControlPro.WPF.Views;

public partial class AddEmployeeDialog : Window
{
    private byte[]? _photoData;
    private List<SubscriptionPlan> _plans = new();
    private IEmployeeService? _employeeService;
    private ILookupService? _lookupService;
    private int? _editId;
    private bool _hasCardNoError;
    private bool _hasPhoneError;
    private string? _pendingSubscriptionType;

    public LanguageManager Lang => LanguageManager.Instance;

    public string FullNameEn => NameEnTextBox.Text.Trim();
    public string FullNameAr => NameArTextBox.Text.Trim();
    public string CardNo => CardNoTextBox.Text.Trim();
    public string SubscriptionType => GetSelectedSubscriptionType();
    public string Phone => PhoneTextBox.Text.Trim();
    public byte[]? PhotoData => _photoData;
    public double PlayerHeight => double.TryParse(HeightTextBox.Text.Trim(), out var h) ? h : 0;
    public double PlayerWeight => double.TryParse(WeightTextBox.Text.Trim(), out var w) ? w : 0;
    public decimal SubscriptionFee => decimal.TryParse(FeeTextBox.Text.Trim(), out var f) ? f : 0;
    public decimal AmountPaid => decimal.TryParse(PaidTextBox.Text.Trim(), out var p) ? p : 0;
    public DateTime StartDate => StartDatePicker.SelectedDate ?? DateTime.Today;
    public DateTime EndDate => EndDatePicker.SelectedDate ?? DateTime.Today.AddMonths(1);
    public string Notes => NotesTextBox.Text.Trim();
    public int MaxVisits => int.TryParse(MaxVisitsTextBox.Text.Trim(), out var v) ? v : 0;

    public AddEmployeeDialog(ILookupService? lookupService = null)
    {
        _lookupService = lookupService;
        InitializeComponent();
        LoadPlans();
        PopulateSubscriptionTypes();
        PopulatePeriods();
        StartDatePicker.SelectedDate = DateTime.Today;
        UpdateRemaining();

        // MaxVisits is always hidden in Add/Edit dialog — set via Assign Card instead
        // MaxVisitsTextBox defaults to "0" in XAML

        if (_lookupService != null)
            _ = LoadPlansFromDbAsync();
    }

    public AddEmployeeDialog(EmployeeDto existing, ILookupService? lookupService = null) : this(lookupService)
    {
        _editId = existing.Id;
        DialogTitle.Text = Lang.EditPlayer;
        Title = Lang.EditPlayer;

        NameEnTextBox.Text = existing.FullNameEn;
        NameArTextBox.Text = existing.FullNameAr;
        CardNoTextBox.Text = existing.CardNo;
        PhoneTextBox.Text = existing.Phone;
        HeightTextBox.Text = existing.Height > 0 ? existing.Height.ToString() : "";
        WeightTextBox.Text = existing.Weight > 0 ? existing.Weight.ToString() : "";
        FeeTextBox.Text = existing.SubscriptionFee > 0 ? existing.SubscriptionFee.ToString() : "";
        PaidTextBox.Text = existing.AmountPaid > 0 ? existing.AmountPaid.ToString() : "";
        NotesTextBox.Text = existing.Notes;

        MaxVisitsTextBox.Text = existing.MaxVisits > 0 ? existing.MaxVisits.ToString() : "0";

        _pendingSubscriptionType = existing.SubscriptionType;
        SelectSubscriptionType(existing.SubscriptionType);

        // Custom period for editing (preserves original dates)
        PeriodCombo.SelectedIndex = PeriodCombo.Items.Count - 1;
        StartDatePicker.SelectedDate = existing.StartDate;
        EndDatePicker.SelectedDate = existing.EndDate;
        FeeTextBox.IsReadOnly = false;

        if (existing.PhotoData != null && existing.PhotoData.Length > 0)
        {
            _photoData = existing.PhotoData;
            LoadPhotoFromBytes(existing.PhotoData);
        }

        UpdateRemaining();
    }

    public void SetValidationService(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    #region Plans & Combos

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
                var json = JsonSerializer.Serialize(_plans, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
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

                // Re-select the subscription type if editing (DB load cleared the combo)
                if (!string.IsNullOrEmpty(_pendingSubscriptionType))
                    SelectSubscriptionType(_pendingSubscriptionType);
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
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Lang.CustomPeriod, Tag = "0" });
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
        SubscriptionTypeCombo.Items.Add(new ComboBoxItem
        {
            Content = type,
            Tag = new SubscriptionPlan { Type = type, MonthlyRate = 0 }
        });
        SubscriptionTypeCombo.SelectedIndex = SubscriptionTypeCombo.Items.Count - 1;
    }

    private string GetSelectedSubscriptionType()
    {
        if (SubscriptionTypeCombo.SelectedItem is ComboBoxItem item && item.Tag is SubscriptionPlan plan)
            return plan.Type;
        return string.Empty;
    }

    private int GetSelectedMonths()
    {
        if (PeriodCombo?.SelectedItem is ComboBoxItem item &&
            item.Tag is string tagStr && int.TryParse(tagStr, out int months))
            return months;
        return 0;
    }

    private SubscriptionPlan? GetSelectedPlan()
    {
        if (SubscriptionTypeCombo?.SelectedItem is ComboBoxItem item && item.Tag is SubscriptionPlan plan)
            return plan;
        return null;
    }

    #endregion

    #region Events

    private void SubscriptionTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RecalcFee();
    }

    private void PeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EndDatePicker == null) return;

        var months = GetSelectedMonths();

        if (months == 0)
        {
            // Custom: enable EndDate, fee editable
            EndDatePicker.IsEnabled = true;
            FeeTextBox.IsReadOnly = false;
        }
        else
        {
            // Preset: auto-calc dates, fee read-only
            EndDatePicker.IsEnabled = false;
            StartDatePicker.SelectedDate = DateTime.Today;
            EndDatePicker.SelectedDate = DateTime.Today.AddMonths(months);
            FeeTextBox.IsReadOnly = true;
            RecalcFee();
        }
    }

    private void FeeTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateRemaining();
    }

    private void PaidTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateRemaining();
    }

    #endregion

    #region Calculations

    private void RecalcFee()
    {
        if (FeeTextBox == null) return;

        var plan = GetSelectedPlan();
        var months = GetSelectedMonths();

        if (plan != null && months > 0 && plan.MonthlyRate > 0)
            FeeTextBox.Text = (plan.MonthlyRate * months).ToString();
    }

    private void UpdateRemaining()
    {
        if (RemainingTextBox == null) return;

        var fee = decimal.TryParse(FeeTextBox.Text.Trim(), out var f) ? f : 0;
        var paid = decimal.TryParse(PaidTextBox.Text.Trim(), out var p) ? p : 0;
        var remaining = fee - paid;

        RemainingTextBox.Text = remaining.ToString();

        // Green if fully paid, red if owes money
        RemainingTextBox.Foreground = remaining <= 0
            ? new SolidColorBrush(Color.FromRgb(0x2E, 0xD4, 0x7A))
            : new SolidColorBrush(Color.FromRgb(0xF7, 0x68, 0x5B));
    }

    #endregion

    #region Photo

    private void BrowsePhoto_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*",
            Title = Lang.SelectPhoto
        };

        if (dialog.ShowDialog() == true)
        {
            _photoData = CompressImage(dialog.FileName);
            LoadPhotoFromBytes(_photoData);
        }
    }

    private void RemovePhoto_Click(object sender, RoutedEventArgs e)
    {
        _photoData = null;
        PhotoImage.Source = null;
    }

    private static byte[] CompressImage(string filePath, int quality = 70, int maxWidth = 400)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();

        BitmapSource source = bitmap;
        if (bitmap.PixelWidth > maxWidth)
        {
            double scale = (double)maxWidth / bitmap.PixelWidth;
            source = new TransformedBitmap(bitmap, new System.Windows.Media.ScaleTransform(scale, scale));
        }

        var encoder = new JpegBitmapEncoder { QualityLevel = quality };
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private void LoadPhotoFromBytes(byte[]? data)
    {
        if (data == null || data.Length == 0)
        {
            PhotoImage.Source = null;
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            using (var ms = new MemoryStream(data))
            {
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
            }
            PhotoImage.Source = bitmap;
        }
        catch
        {
            PhotoImage.Source = null;
        }
    }

    #endregion

    #region Real-time Duplicate Validation

    private async void CardNoTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var cardNo = CardNoTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(cardNo) || _employeeService == null)
        {
            ClearFieldError(CardNoTextBox, CardNoError);
            _hasCardNoError = false;
            return;
        }

        try
        {
            var isDuplicate = await _employeeService.IsCardNoDuplicateAsync(cardNo, _editId);
            if (isDuplicate)
            {
                ShowFieldError(CardNoTextBox, CardNoError,
                    string.Format(Lang.DuplicateCardNo, cardNo));
                _hasCardNoError = true;
            }
            else
            {
                ClearFieldError(CardNoTextBox, CardNoError);
                _hasCardNoError = false;
            }
        }
        catch
        {
            ClearFieldError(CardNoTextBox, CardNoError);
            _hasCardNoError = false;
        }
    }

    private async void PhoneTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var phone = PhoneTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(phone) || _employeeService == null)
        {
            ClearFieldError(PhoneTextBox, PhoneError);
            _hasPhoneError = false;
            return;
        }

        try
        {
            var isDuplicate = await _employeeService.IsPhoneDuplicateAsync(phone, _editId);
            if (isDuplicate)
            {
                ShowFieldError(PhoneTextBox, PhoneError,
                    string.Format(Lang.DuplicatePhone, phone));
                _hasPhoneError = true;
            }
            else
            {
                ClearFieldError(PhoneTextBox, PhoneError);
                _hasPhoneError = false;
            }
        }
        catch
        {
            ClearFieldError(PhoneTextBox, PhoneError);
            _hasPhoneError = false;
        }
    }

    private async void NameEnTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        var name = NameEnTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || _employeeService == null)
        {
            NameEnWarning.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            var isDuplicate = await _employeeService.IsNameDuplicateAsync(name, _editId);
            if (isDuplicate)
            {
                NameEnWarning.Text = string.Format(Lang.DuplicateNameWarning, name);
                NameEnWarning.Visibility = Visibility.Visible;
            }
            else
            {
                NameEnWarning.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            NameEnWarning.Visibility = Visibility.Collapsed;
        }
    }

    private static void ShowFieldError(TextBox textBox, TextBlock errorBlock, string message)
    {
        textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF7, 0x68, 0x5B));
        errorBlock.Text = message;
        errorBlock.Visibility = Visibility.Visible;
    }

    private static void ClearFieldError(TextBox textBox, TextBlock errorBlock)
    {
        textBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x50));
        errorBlock.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Input Filtering

    private void NameEnTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow only English letters, spaces, hyphens, apostrophes
        e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-Z\s\-']+$");
    }

    private void NameArTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow only Arabic letters, spaces, common Arabic diacritics
        e.Handled = !Regex.IsMatch(e.Text, @"^[\u0600-\u06FF\u0750-\u077F\uFB50-\uFDFF\uFE70-\uFEFF\s\-]+$");
    }

    private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow only digits
        e.Handled = !Regex.IsMatch(e.Text, @"^[0-9]+$");
    }

    private void DecimalOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow digits and one decimal point
        var textBox = sender as TextBox;
        if (e.Text == "." && textBox != null && !textBox.Text.Contains('.'))
        {
            e.Handled = false;
            return;
        }
        e.Handled = !Regex.IsMatch(e.Text, @"^[0-9]+$");
    }

    private void PhoneTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow only digits and + (for international prefix)
        e.Handled = !Regex.IsMatch(e.Text, @"^[0-9+]+$");
    }

    /// <summary>Strips invalid characters from pasted text (Ctrl+V bypass prevention).</summary>
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

    private void PhoneOnly_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));
            var cleaned = Regex.Replace(text ?? "", @"[^0-9+]", "");
            if (string.IsNullOrEmpty(cleaned)) { e.CancelCommand(); return; }
            if (cleaned != text)
            {
                e.CancelCommand();
                if (sender is TextBox tb) tb.SelectedText = cleaned;
            }
        }
        else e.CancelCommand();
    }

    private void LettersEnOnly_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));
            var cleaned = Regex.Replace(text ?? "", @"[^a-zA-Z\s\-']", "");
            if (string.IsNullOrEmpty(cleaned)) { e.CancelCommand(); return; }
            if (cleaned != text)
            {
                e.CancelCommand();
                if (sender is TextBox tb) tb.SelectedText = cleaned;
            }
        }
        else e.CancelCommand();
    }

    private void LettersArOnly_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));
            var cleaned = Regex.Replace(text ?? "", @"[^\u0600-\u06FF\u0750-\u077F\uFB50-\uFDFF\uFE70-\uFEFF\s\-]", "");
            if (string.IsNullOrEmpty(cleaned)) { e.CancelCommand(); return; }
            if (cleaned != text)
            {
                e.CancelCommand();
                if (sender is TextBox tb) tb.SelectedText = cleaned;
            }
        }
        else e.CancelCommand();
    }

    private void DecimalOnly_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));
            var cleaned = Regex.Replace(text ?? "", @"[^0-9.]", "");
            if (string.IsNullOrEmpty(cleaned)) { e.CancelCommand(); return; }
            // Remove extra decimal points
            var firstDot = cleaned.IndexOf('.');
            if (firstDot >= 0)
                cleaned = cleaned[..(firstDot + 1)] + cleaned[(firstDot + 1)..].Replace(".", "");
            if (sender is TextBox tb && tb.Text.Contains('.'))
                cleaned = cleaned.Replace(".", "");
            if (cleaned != text)
            {
                e.CancelCommand();
                if (sender is TextBox textBox) textBox.SelectedText = cleaned;
            }
        }
        else e.CancelCommand();
    }

    #endregion

    #region Validation & Submit

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var errors = new List<string>();

        // At least one name (EN or AR) is required; if only one is filled, copy it to the other
        var hasEn = !string.IsNullOrWhiteSpace(NameEnTextBox.Text);
        var hasAr = !string.IsNullOrWhiteSpace(NameArTextBox.Text);
        if (!hasEn && !hasAr)
            errors.Add(Lang.AtLeastOneNameRequired);
        else if (hasEn && !hasAr)
            NameArTextBox.Text = NameEnTextBox.Text.Trim();
        else if (hasAr && !hasEn)
            NameEnTextBox.Text = NameArTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(CardNoTextBox.Text))
            errors.Add(Lang.CardNoRequired);
        else if (_hasCardNoError)
            errors.Add(string.Format(Lang.DuplicateCardNo, CardNoTextBox.Text.Trim()));

        if (string.IsNullOrWhiteSpace(PhoneTextBox.Text))
            errors.Add(Lang.PhoneRequired);
        else if (_hasPhoneError)
            errors.Add(string.Format(Lang.DuplicatePhone, PhoneTextBox.Text.Trim()));

        if (SubscriptionTypeCombo.SelectedIndex < 0)
            errors.Add(Lang.SubscriptionRequired);

        if (PeriodCombo.SelectedIndex < 0)
            errors.Add(Lang.PeriodRequired);

        if (string.IsNullOrWhiteSpace(FeeTextBox.Text) || SubscriptionFee <= 0)
            errors.Add(Lang.FeeRequired);

        if (_photoData == null || _photoData.Length == 0)
            errors.Add(Lang.PhotoRequired);

        if (!string.IsNullOrWhiteSpace(HeightTextBox.Text))
        {
            if (!double.TryParse(HeightTextBox.Text.Trim(), out var h) || h < 50 || h > 250)
                errors.Add(Lang.HeightInvalid);
        }

        if (!string.IsNullOrWhiteSpace(WeightTextBox.Text))
        {
            if (!double.TryParse(WeightTextBox.Text.Trim(), out var w) || w < 20 || w > 300)
                errors.Add(Lang.WeightInvalid);
        }

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

    #endregion
}

public class SubscriptionPlan
{
    public string Type { get; set; } = string.Empty;
    public decimal MonthlyRate { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? DisplayName { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string Label => DisplayName ?? Type;
}
