using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Enums;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

/// <summary>
/// Anonymous daily-entry pass: a fixed price (from the "Daily Pass" subscription plan),
/// valid today only. The cashier issues either a QR ticket (printed) or a temporary
/// physical card (scanned, handed out, returned at exit). Income is recorded under
/// "Daily Pass".
/// </summary>
public partial class DailyPassDialog : Window
{
    private readonly IQrPassService _qrPassService;
    private readonly ILookupService _lookupService;
    private readonly IFinanceService _financeService;
    private readonly IEmployeeService _employeeService;
    private readonly bool _ar;
    private decimal _price;
    private string _category = "Daily Pass";                 // income category for the chosen type
    private List<DailyType> _types = new();                  // all daily-pass plans (name + price)
    private sealed record DailyType(string Name, decimal Price);

    public DailyPassDialog(IQrPassService qrPassService, ILookupService lookupService,
        IFinanceService financeService, IEmployeeService employeeService)
    {
        _qrPassService = qrPassService;
        _lookupService = lookupService;
        _financeService = financeService;
        _employeeService = employeeService;
        InitializeComponent();

        _ar = LanguageManager.Instance.IsArabic;
        DateText.Text = DateTime.Now.ToString("yyyy-MM-dd");
        GymNameText.Text = GymProfile.DisplayName;
        PriceLabel.Text = _ar ? "سعر اليوم" : "Today's price";
        QrButtonText.Text = _ar ? "تذكرة QR" : "QR Ticket";
        TempButtonText.Text = _ar ? "كارت مؤقت" : "Temp Card";
        ManualButtonText.Text = _ar ? "دخول يدوي (تحصيل فقط)" : "Manual Entry (collect only)";
        ScanHint.Text = _ar
            ? "امسح الكارت على القارئ أو اكتب رقمه ثم اضغط إصدار"
            : "Scan the card on the reader (or type its number) then press Issue";
        IssueButton.Content = _ar ? "إصدار" : "Issue";
        CloseText.Text = _ar ? "إغلاق" : "Close";

        Loaded += async (_, _) => { await LoadPriceAsync(); await RefreshTodayAsync(); RefreshOutList(); };
    }

    private async System.Threading.Tasks.Task LoadPriceAsync()
    {
        try
        {
            var plans = await _lookupService.GetActiveSubscriptionPlansAsync();
            // A gym can define MORE THAN ONE daily-entry plan (different tiers/prices). List them all;
            // each entry is recorded under its own name so the owner sees revenue per tier.
            _types = plans
                .Where(p => p.NameEn.Trim().StartsWith("Daily", StringComparison.OrdinalIgnoreCase)
                         || p.NameAr.Contains("يومي"))
                .Select(p => new DailyType(string.IsNullOrWhiteSpace(p.NameEn) ? p.NameAr : p.NameEn.Trim(), p.Price))
                .ToList();
        }
        catch { _types = new(); }

        if (_types.Count > 1)
        {
            TypeCombo.ItemsSource = _types.Select(t => $"{t.Name} — {t.Price:N0}").ToList();
            TypeCombo.SelectedIndex = 0;
            TypeCombo.Visibility = Visibility.Visible;   // triggers SelectionChanged -> ApplyType
        }
        else
        {
            ApplyType(_types.FirstOrDefault());
        }
    }

    private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TypeCombo.SelectedIndex >= 0 && TypeCombo.SelectedIndex < _types.Count)
            ApplyType(_types[TypeCombo.SelectedIndex]);
    }

    private void ApplyType(DailyType? type)
    {
        _price = type?.Price ?? 0m;
        _category = type?.Name ?? "Daily Pass";
        PriceText.Text = _price.ToString("N0");
        if (_price > 0)
            PriceText.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryLightBrush");
        else
            PriceText.Foreground = System.Windows.Media.Brushes.OrangeRed;
        if (_price <= 0)
            TodaySummaryText.Text = _ar
                ? "⚠ حدّد سعر خطة \"دخول يومي\" من لوحة الإدارة أولاً"
                : "⚠ Set the \"Daily Pass\" plan price in Admin first";
        else
            _ = RefreshTodayAsync();
    }

    private async System.Threading.Tasks.Task RefreshTodayAsync()
    {
        try
        {
            var today = DateTime.Today;
            var items = await _financeService.GetTransactionsAsync(
                TransactionType.Income, today, today.AddDays(1).AddTicks(-1));
            // Match any daily-pass category (each tier has its own name), then break down per type.
            var cats = _types.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (cats.Count == 0) cats.Add("Daily Pass");
            var daily = items.Where(t => cats.Contains(t.Category)).ToList();
            var count = daily.Count;
            var total = daily.Sum(t => t.Amount);

            var header = _ar ? $"اليوم: {count} دخول — الإجمالي {total:N0}" : $"Today: {count} entries — total {total:N0}";
            if (_types.Count > 1)
            {
                var perType = daily.GroupBy(t => t.Category)
                    .Select(g => $"{g.Key}: {g.Count()} — {g.Sum(x => x.Amount):N0}");
                header += "  (" + string.Join(" | ", perType) + ")";
            }
            TodaySummaryText.Text = header;
        }
        catch { /* summary is best-effort */ }
    }

    private async void QrTicketClick(object sender, RoutedEventArgs e)
    {
        if (!EnsurePrice()) return;
        QrButton.IsEnabled = false;
        try
        {
            var pass = await _qrPassService.CreatePassAsync(
                playerName: _category,
                phone: "",
                fee: _price,
                maxUses: 2,      // one entry + one exit — stops a found/shared ticket being reused
                validDays: 1,    // today only
                incomeCategory: _category);

            // Bind the code to the gate for today only (date-enforced + 2 uses). Check the result:
            // if it reached NO device, warn the cashier — the QR may not open the door.
            try
            {
                var (okCnt, failCnt, totalCnt, pushErrors) = await _employeeService.PushTempCardToDevicesAsync(
                    pass.PassCode, pass.ValidTo, "01010000", maxUses: 2);
                if (totalCnt > 0 && okCnt == 0)
                {
                    CustomMessageBox.Show(
                        (_ar ? "تنبيه: لم يصل رمز الدخول إلى البوابة (تحقّق من اتصال الجهاز). قد لا يفتح الـQR الباب — أعد المزامنة أو جرّب مجدداً."
                             : "Warning: the pass code didn't reach the gate (check the device connection). The QR may not open the door — re-sync or try again.")
                        + (pushErrors.Count > 0 ? "\n\n" + string.Join("\n", pushErrors) : ""),
                        LanguageManager.Instance.DailyPass, MsgType.Warning, this);
                }
            }
            catch { /* no devices configured or a transient error — the pre-synced pool code still applies */ }

            var qrDialog = new QrCodeDisplayDialog(pass) { Owner = this };
            qrDialog.ShowDialog();
            await RefreshTodayAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, LanguageManager.Instance.DailyPass, MsgType.Error, this);
        }
        finally { QrButton.IsEnabled = true; }
    }

    private async void ManualEntryClick(object sender, RoutedEventArgs e)
    {
        if (!EnsurePrice()) return;
        if (!CustomMessageBox.Confirm(
            _ar ? $"تحصيل دخول يومي بمبلغ {_price:N0}؟ (الكابتن يُدخل الزائر يدوياً)"
                : $"Collect a daily pass of {_price:N0}? (the captain admits the guest manually)",
            LanguageManager.Instance.DailyPass))
            return;

        ManualButton.IsEnabled = false;
        try
        {
            // No QR / card issued — the captain admits the guest (joker card / push button).
            // We only record the fee so it shows in today's takings and Finance.
            await _financeService.RecordIncomeAsync(_category, _price, $"{_category} - manual entry");
            await RefreshTodayAsync();
            CustomMessageBox.Show(
                _ar ? "تم تسجيل الدخول اليومي." : "Daily pass recorded.",
                LanguageManager.Instance.DailyPass, MsgType.Success, this);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, LanguageManager.Instance.DailyPass, MsgType.Error, this);
        }
        finally { ManualButton.IsEnabled = true; }
    }

    private void TempCardToggle(object sender, RoutedEventArgs e)
    {
        if (!EnsurePrice()) return;
        TempPanel.Visibility = TempPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed : Visibility.Visible;
        if (TempPanel.Visibility == Visibility.Visible)
        {
            RefreshOutList();
            CardNumberBox.Focus();
        }
    }

    private void CardNumberBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) IssueCardClick(sender, e);
    }

    private async void IssueCardClick(object sender, RoutedEventArgs e)
    {
        if (!EnsurePrice()) return;
        var card = CardNumberBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(card))
        {
            CardNumberBox.Focus();
            return;
        }
        if (DailyTempCardStore.IsOut(card))
        {
            CustomMessageBox.Show(
                _ar ? "هذا الكارت مُصدَر بالفعل ولم يُعَد بعد." : "This card is already issued and not yet returned.",
                LanguageManager.Instance.DailyPass, MsgType.Warning, this);
            return;
        }
        IssueButton.IsEnabled = false;
        try
        {
            // Program the card on the gate, valid until tonight only → controller auto-rejects
            // it tomorrow. Card returned today is expired immediately via ReturnCardClick.
            var validTo = DateTime.Today.AddDays(1).AddSeconds(-1); // today 23:59:59
            var (ok, fail, total, errors) = await _employeeService.PushTempCardToDevicesAsync(card, validTo, "01010000", maxUses: 2);
            if (ok == 0)
            {
                CustomMessageBox.Show(
                    (_ar ? "تعذّر برمجة الكارت على البوابة (غير متصلة؟). لم يتم الإصدار."
                         : "Could not program the card on the gate (offline?). Not issued.")
                    + (errors.Count > 0 ? "\n\n" + string.Join("\n", errors) : ""),
                    LanguageManager.Instance.DailyPass, MsgType.Error, this);
                return;
            }

            await _financeService.RecordIncomeAsync(_category, _price, $"{_category} - temp card {card}");
            DailyTempCardStore.Issue(card, _price);
            CardNumberBox.Clear();
            CardNumberBox.Focus();
            RefreshOutList();
            await RefreshTodayAsync();

            // Optional 80mm receipt — gym name + bracelet no. + dates + price. Anonymous (no member
            // name). Prints straight to the default printer (the gyms set an 80mm as default).
            if (CustomMessageBox.Confirm(
                    _ar ? "طباعة وصل الدخول اليومي؟" : "Print the daily-pass receipt?",
                    LanguageManager.Instance.DailyPass, MsgType.Info, this))
            {
                try { ThermalReceipt.PrintDailyPass(card, DateTime.Today, validTo, _price); }
                catch (Exception pe) { CustomMessageBox.Show(pe.Message, LanguageManager.Instance.DailyPass, MsgType.Error, this); }
            }

            if (fail > 0)
                CustomMessageBox.Show(
                    _ar ? $"تم الإصدار، لكن لم تصل إلى {fail} جهاز." : $"Issued, but {fail} device(s) were not reached.",
                    LanguageManager.Instance.DailyPass, MsgType.Warning, this);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, LanguageManager.Instance.DailyPass, MsgType.Error, this);
        }
        finally { IssueButton.IsEnabled = true; }
    }

    private async void ReturnCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string card)
        {
            try { await _employeeService.ExpireTempCardOnDevicesAsync(card); }
            catch { /* card also auto-expires tonight regardless */ }
            DailyTempCardStore.Return(card);
            RefreshOutList();
        }
    }

    private void RefreshOutList()
    {
        var items = DailyTempCardStore.LoadOut()
            .OrderByDescending(c => c.IssuedAt)
            .Select(c => new OutRow
            {
                CardNumber = c.CardNumber,
                IssuedAtText = c.IssuedAt.ToString("HH:mm"),
                ReturnLabel = _ar ? "إرجاع" : "Return"
            })
            .ToList();
        OutList.ItemsSource = items;
        OutHeader.Text = (_ar ? "كروت بالخارج" : "Cards out") + $" ({items.Count})";
    }

    private bool EnsurePrice()
    {
        if (_price > 0) return true;
        CustomMessageBox.Show(
            _ar ? "حدّد سعر خطة \"Daily Pass\" من لوحة الإدارة أولاً."
                : "Set the \"Daily Pass\" plan price in Admin first.",
            LanguageManager.Instance.DailyPass, MsgType.Warning, this);
        return false;
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private class OutRow
    {
        public string CardNumber { get; set; } = "";
        public string IssuedAtText { get; set; } = "";
        public string ReturnLabel { get; set; } = "";
    }
}
