using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AccessControlPro.Application.DTOs;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class RenewSubscriptionDialog : Window
{
    private List<SubscriptionPlan> _plans = new();

    public LanguageManager Lang => LanguageManager.Instance;

    public string SelectedSubscriptionType { get; private set; } = string.Empty;
    public int SelectedMonths { get; private set; }
    public decimal NewFee => decimal.TryParse(FeeTextBox.Text.Trim(), out var f) ? f : 0;
    public decimal NewAmountPaid => decimal.TryParse(PaidTextBox.Text.Trim(), out var p) ? p : 0;

    public RenewSubscriptionDialog(EmployeeDto employee)
    {
        InitializeComponent();
        PlayerNameText.Text = $"{employee.FullNameEn} ({employee.CardNo})";
        LoadPlans();
        PopulateSubscriptionTypes();
        PopulatePeriods();
        SelectSubscriptionType(employee.SubscriptionType);
        UpdateRemaining();
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

    private void PopulateSubscriptionTypes()
    {
        SubscriptionTypeCombo.Items.Clear();
        foreach (var plan in _plans)
            SubscriptionTypeCombo.Items.Add(new ComboBoxItem { Content = plan.Type, Tag = plan });
    }

    private void PopulatePeriods()
    {
        PeriodCombo.Items.Clear();
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Lang.Month1, Tag = "1" });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Lang.Months3, Tag = "3" });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Lang.Months6, Tag = "6" });
        PeriodCombo.Items.Add(new ComboBoxItem { Content = Lang.Months12, Tag = "12" });
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
        RecalcFee();
    }

    private void RecalcFee()
    {
        if (FeeTextBox == null) return;
        var plan = GetSelectedPlan();
        var months = GetSelectedMonths();
        if (plan != null && months > 0 && plan.MonthlyRate > 0)
            FeeTextBox.Text = (plan.MonthlyRate * months).ToString();
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

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var errors = new List<string>();

        if (SubscriptionTypeCombo.SelectedIndex < 0)
            errors.Add(Lang.SubscriptionRequired);

        if (PeriodCombo.SelectedIndex < 0)
            errors.Add(Lang.PeriodRequired);

        if (string.IsNullOrWhiteSpace(FeeTextBox.Text) || NewFee <= 0)
            errors.Add(Lang.FeeRequired);

        if (errors.Count > 0)
        {
            var message = string.Join("\n", errors.Select(err => $"  \u2022  {err}"));
            CustomMessageBox.Show(message, Lang.ValidationTitle, MsgType.Warning, this);
            return;
        }

        var plan = GetSelectedPlan();
        SelectedSubscriptionType = plan?.Type ?? string.Empty;
        SelectedMonths = GetSelectedMonths();
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
