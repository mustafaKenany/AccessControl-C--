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
    private readonly bool _ar;
    private decimal _price;

    public DailyPassDialog(IQrPassService qrPassService, ILookupService lookupService, IFinanceService financeService)
    {
        _qrPassService = qrPassService;
        _lookupService = lookupService;
        _financeService = financeService;
        InitializeComponent();

        _ar = LanguageManager.Instance.IsArabic;
        DateText.Text = DateTime.Now.ToString("yyyy-MM-dd");
        GymNameText.Text = GymProfile.DisplayName;
        PriceLabel.Text = _ar ? "سعر اليوم" : "Today's price";
        QrButtonText.Text = _ar ? "تذكرة QR" : "QR Ticket";
        TempButtonText.Text = _ar ? "كارت مؤقت" : "Temp Card";
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
            var daily = plans.FirstOrDefault(p =>
                p.NameEn.Trim().Equals("Daily Pass", StringComparison.OrdinalIgnoreCase)
                || p.NameEn.Trim().Equals("Daily", StringComparison.OrdinalIgnoreCase)
                || p.NameAr.Contains("يومي"));
            _price = daily?.Price ?? 0m;
        }
        catch { _price = 0m; }

        PriceText.Text = _price.ToString("N0");
        if (_price <= 0)
        {
            PriceText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            TodaySummaryText.Text = _ar
                ? "⚠ حدّد سعر خطة \"Daily Pass\" من لوحة الإدارة أولاً"
                : "⚠ Set the \"Daily Pass\" plan price in Admin first";
        }
    }

    private async System.Threading.Tasks.Task RefreshTodayAsync()
    {
        if (_price <= 0) return;
        try
        {
            var today = DateTime.Today;
            var items = await _financeService.GetTransactionsAsync(
                TransactionType.Income, today, today.AddDays(1).AddTicks(-1));
            var daily = items.Where(t => t.Category == "Daily Pass").ToList();
            var count = daily.Count;
            var total = daily.Sum(t => t.Amount);
            TodaySummaryText.Text = _ar
                ? $"اليوم: {count} دخول — الإجمالي {total:N0}"
                : $"Today: {count} entries — total {total:N0}";
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
                playerName: _ar ? "دخول يومي" : "Daily Pass",
                phone: "",
                fee: _price,
                maxUses: 2,      // enter + exit
                validDays: 1);   // today only

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
        try
        {
            await _financeService.RecordIncomeAsync("Daily Pass", _price, $"Daily Pass - temp card {card}");
            DailyTempCardStore.Issue(card, _price);
            CardNumberBox.Clear();
            CardNumberBox.Focus();
            RefreshOutList();
            await RefreshTodayAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, LanguageManager.Instance.DailyPass, MsgType.Error, this);
        }
    }

    private void ReturnCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string card)
        {
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
