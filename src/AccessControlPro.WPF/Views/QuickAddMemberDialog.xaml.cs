using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
// Views already defines its own local "DomainPlan" (in AddEmployeeDialog) — alias the Domain
// entity so we bind against the DB plan (NameEn/NameAr/Duration/DurationType/Price/MaxVisits).
using DomainPlan = AccessControlPro.Domain.Entities.SubscriptionPlan;

namespace AccessControlPro.WPF.Views;

/// <summary>
/// The 3-tap quick add: type the name, tap a plan tile, scan/tap the card — done. Numeric fields
/// (phone / card / paid) drive an on-screen keypad so the operator barely needs the keyboard.
/// Reuses the SAME creation pipeline as the full dialog: it only collects data and exposes it; the
/// caller (EmployeesViewModel) runs AddEmployeeAsync + AssignCard + gate sync as usual.
/// Photo is intentionally optional here (can be added later via full edit) to keep it fast.
/// </summary>
public partial class QuickAddMemberDialog : Window
{
    private readonly ILookupService _lookupService;
    private DomainPlan? _selectedPlan;
    private Button? _selectedTile;

    // ── Collected result (mirrors the fields the full AddEmployeeDialog exposes) ──
    public string FullNameEn { get; private set; } = "";
    public string FullNameAr { get; private set; } = "";
    public string CardNo { get; private set; } = "";
    public string Phone { get; private set; } = "";
    public byte[]? PhotoData => null;
    public string SubscriptionType { get; private set; } = "";
    public decimal SubscriptionFee { get; private set; }
    public decimal AmountPaid { get; private set; }
    public decimal Discount => 0m;
    public DateTime StartDate { get; private set; } = DateTime.Today;
    public DateTime EndDate { get; private set; } = DateTime.Today;
    public int MaxVisits { get; private set; }
    public double PlayerHeight => 0;
    public double PlayerWeight => 0;
    public string Notes => "";

    private LanguageManager Lang => LanguageManager.Instance;

    public QuickAddMemberDialog(ILookupService lookupService)
    {
        _lookupService = lookupService;
        InitializeComponent();
        FlowDirection = Lang.FlowDirection;
        ApplyLanguage();
        Loaded += async (_, _) => await LoadPlansAsync();
    }

    private void ApplyLanguage()
    {
        bool ar = Lang.IsArabic;
        TitleText.Text = ar ? "إضافة عضو سريع" : "Quick add member";
        NameLabel.Text = ar ? "الاسم" : "Name";
        PhoneLabel.Text = ar ? "الهاتف" : "Phone";
        CardLabel.Text = ar ? "رقم الكارت (امسح أو أدخل)" : "Card number (scan or type)";
        PaidLabel.Text = ar ? "المبلغ المدفوع" : "Amount paid";
        PlansLabel.Text = ar ? "اختر الخطة" : "Choose a plan";
        SummaryPlan.Text = ar ? "لم يتم اختيار خطة" : "No plan selected";
        SaveText.Text = ar ? "حفظ العضو" : "Save member";
        CancelText.Text = ar ? "إلغاء" : "Cancel";
    }

    private async System.Threading.Tasks.Task LoadPlansAsync()
    {
        try
        {
            var plans = (await _lookupService.GetActiveSubscriptionPlansAsync())
                .Where(p => p.IsActive)
                .OrderBy(p => p.SortOrder).ToList();

            PlansPanel.Items.Clear();
            foreach (var plan in plans)
                PlansPanel.Items.Add(BuildTile(plan));

            NameBox.Focus();
        }
        catch { /* leave empty — the owner can still use the full add dialog */ }
    }

    private Button BuildTile(DomainPlan plan)
    {
        bool ar = Lang.IsArabic;
        var name = ar && !string.IsNullOrWhiteSpace(plan.NameAr) ? plan.NameAr : plan.NameEn;
        var durLabel = DurationLabel(plan);

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = name, FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis
        });
        panel.Children.Add(new TextBlock
        {
            Text = durLabel, FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)), Margin = new Thickness(0, 3, 0, 0)
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{plan.Price:N0} {Lang.IQD}", FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0xD4, 0x7A)), Margin = new Thickness(0, 4, 0, 0)
        });

        var border = new Border
        {
            CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 10, 12, 10),
            Background = new SolidColorBrush(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x44, 0xA1, 0xA0)),
            BorderThickness = new Thickness(1.5), Child = panel
        };

        var btn = new Button
        {
            Content = border, Width = 132, Margin = new Thickness(4), Cursor = Cursors.Hand,
            Tag = plan, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Template = (ControlTemplate)Resources["TileTemplate"]!
        };
        btn.Click += Tile_Click;
        return btn;
    }

    private string DurationLabel(DomainPlan plan)
    {
        bool ar = Lang.IsArabic;
        if (string.Equals(plan.DurationType, "Unlimited", StringComparison.OrdinalIgnoreCase))
            return ar ? "غير محدود" : "Unlimited";
        if (string.Equals(plan.DurationType, "Months", StringComparison.OrdinalIgnoreCase))
            return ar ? $"{plan.Duration} شهر" : $"{plan.Duration} months";
        return ar ? $"{plan.Duration} يوم" : $"{plan.Duration} days";
    }

    private void Tile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DomainPlan plan) return;

        // Highlight the chosen tile, un-highlight the previous one.
        if (_selectedTile?.Content is Border prev)
        {
            prev.BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0x44, 0xA1, 0xA0));
            prev.Background = new SolidColorBrush(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF));
        }
        if (btn.Content is Border cur)
        {
            cur.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xD4, 0x7A));
            cur.Background = new SolidColorBrush(Color.FromArgb(0x22, 0x2E, 0xD4, 0x7A));
        }
        _selectedTile = btn;
        _selectedPlan = plan;

        StartDate = DateTime.Today;
        EndDate = ComputeEnd(plan, StartDate);
        SubscriptionType = plan.NameEn;
        SubscriptionFee = plan.Price;
        MaxVisits = plan.MaxVisits;

        // Default the paid amount to the full fee (paid in full) — the operator can adjust it.
        PaidBox.Text = plan.Price.ToString("0");

        bool ar = Lang.IsArabic;
        var name = ar && !string.IsNullOrWhiteSpace(plan.NameAr) ? plan.NameAr : plan.NameEn;
        SummaryPlan.Text = $"{name} — {plan.Price:N0} {Lang.IQD}";
        SummaryDates.Text = (ar ? "من " : "From ") + StartDate.ToString("yyyy-MM-dd") +
                            (ar ? " إلى " : " to ") + EndDate.ToString("yyyy-MM-dd");
    }

    private static DateTime ComputeEnd(DomainPlan plan, DateTime start)
    {
        if (string.Equals(plan.DurationType, "Unlimited", StringComparison.OrdinalIgnoreCase))
            return start.AddYears(50);
        if (string.Equals(plan.DurationType, "Months", StringComparison.OrdinalIgnoreCase))
            return start.AddMonths(Math.Max(1, plan.Duration));
        return start.AddDays(Math.Max(1, plan.Duration));
    }

    // ── Keypad targeting ──
    private void NumericField_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb) Keypad.TargetBox = tb;
    }

    private void NameBox_GotFocus(object sender, RoutedEventArgs e)
    {
        // Name is letters — don't let the numeric keypad type into it.
        Keypad.TargetBox = null;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        bool ar = Lang.IsArabic;
        var errors = new List<string>();

        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
            errors.Add(ar ? "الاسم مطلوب" : "Name is required");
        if (string.IsNullOrWhiteSpace(CardBox.Text))
            errors.Add(ar ? "رقم الكارت مطلوب" : "Card number is required");
        if (_selectedPlan == null)
            errors.Add(ar ? "اختر خطة اشتراك" : "Choose a subscription plan");

        decimal paid = 0;
        if (!string.IsNullOrWhiteSpace(PaidBox.Text) && !decimal.TryParse(PaidBox.Text.Trim(), out paid))
            errors.Add(ar ? "المبلغ المدفوع غير صحيح" : "Amount paid is invalid");

        if (errors.Count > 0)
        {
            CustomMessageBox.Show(string.Join("\n", errors.Select(x => $"  •  {x}")),
                ar ? "تحقق" : "Check", MsgType.Warning, this);
            return;
        }

        FullNameAr = name;
        FullNameEn = name; // service copies EN↔AR anyway; set both so reports never show a blank
        Phone = PhoneBox.Text.Trim();
        CardNo = CardBox.Text.Trim();
        AmountPaid = paid;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
