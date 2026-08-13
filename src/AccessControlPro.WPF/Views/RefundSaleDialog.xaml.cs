using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

/// <summary>Pick a recent sale and choose how many of each line to refund. Returns the chosen
/// receipt + the per-line quantities; the service caps them to what remains refundable.
/// Ported from the HikVision fork.</summary>
public partial class RefundSaleDialog : Window
{
    public string SelectedReceiptNo { get; private set; } = string.Empty;
    public List<CartItemDto> RefundItems { get; private set; } = new();

    public RefundSaleDialog(List<PosSaleDto> sales)
    {
        InitializeComponent();
        var lang = LanguageManager.Instance;
        bool ar = lang.IsArabic;

        TitleText.Text = ar ? "استرجاع من فاتورة" : "Refund from a sale";
        PickText.Text = ar ? "اختر الفاتورة الأصلية:" : "Pick the original sale:";
        LinesText.Text = ar ? "حدّد الكمية المسترجعة لكل صنف:" : "Set the quantity to refund per item:";
        OkText.Text = ar ? "تأكيد الاسترجاع" : "Refund";

        ColTime.Header = ar ? "الوقت" : "Time";
        ColItems.Header = ar ? "الأصناف" : "Items";
        ColTotal.Header = ar ? "الإجمالي" : "Total";
        ColRefundable.Header = ar ? "القابل للاسترجاع" : "Refundable";
        ColProduct.Header = ar ? "الصنف" : "Item";
        ColPrice.Header = ar ? "السعر" : "Price";
        ColSold.Header = ar ? "المباع" : "Sold";
        ColRemaining.Header = ar ? "المتبقي" : "Remaining";
        ColRefundNow.Header = ar ? "استرجاع" : "Refund";

        // Only show sales that still have something refundable.
        SalesGrid.ItemsSource = sales.Where(s => !s.FullyRefunded).ToList();
    }

    private void SalesGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        LinesGrid.ItemsSource = (SalesGrid.SelectedItem as PosSaleDto)?.Lines
            .Where(l => l.RefundableQty > 0).ToList();
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        LinesGrid.CommitEdit();   // flush the in-progress cell edit into the bound objects
        var lang = LanguageManager.Instance;

        if (SalesGrid.SelectedItem is not PosSaleDto sale)
        {
            CustomMessageBox.Show(lang.IsArabic ? "اختر فاتورة أول." : "Select a sale first.",
                lang.ValidationTitle, MsgType.Warning, this);
            return;
        }

        var items = new List<CartItemDto>();
        foreach (var line in sale.Lines)
        {
            if (line.RefundNowQty <= 0) continue;
            if (line.RefundNowQty > line.RefundableQty)
            {
                CustomMessageBox.Show(
                    lang.IsArabic ? $"'{line.ProductName}': لا يمكن استرجاع أكثر من {line.RefundableQty}."
                                  : $"'{line.ProductName}': cannot refund more than {line.RefundableQty}.",
                    lang.ValidationTitle, MsgType.Warning, this);
                return;
            }
            items.Add(new CartItemDto { ProductId = line.ProductId, ProductName = line.ProductName, Price = line.UnitPrice, Quantity = line.RefundNowQty });
        }

        if (items.Count == 0)
        {
            CustomMessageBox.Show(lang.IsArabic ? "حدّد كمية صنف واحد على الأقل." : "Set a quantity for at least one item.",
                lang.ValidationTitle, MsgType.Warning, this);
            return;
        }

        SelectedReceiptNo = sale.ReceiptNo;
        RefundItems = items;
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
