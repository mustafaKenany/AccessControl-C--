using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AccessControlPro.Application.DTOs;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;

namespace AccessControlPro.Admin.Views;

/// <summary>Physical stock-take: edit the counted quantity per product, then apply. Returns the
/// (ProductId, CountedStock) pairs; the service writes signed Adjustment movements atomically.</summary>
public partial class StockTakeDialog : Window
{
    private readonly List<StockTakeLineDto> _lines;
    public List<(int ProductId, int CountedStock)> Counts { get; private set; } = new();

    public StockTakeDialog(List<StockTakeLineDto> lines)
    {
        InitializeComponent();
        _lines = lines;
        var lang = LanguageManager.Instance;
        bool ar = lang.IsArabic;

        TitleText.Text = ar ? "جرد المخزون" : "Stock Take";
        HintText.Text = ar ? "عدّل الكمية المجرودة لكل صنف ثم طبّق:" : "Edit the counted quantity per item, then apply:";
        OkBtn.Content = ar ? "تطبيق الجرد" : "Apply";
        CancelBtn.Content = lang.Cancel;

        ColName.Header = ar ? "الصنف" : "Item";
        ColCategory.Header = ar ? "الفئة" : "Category";
        ColSystem.Header = ar ? "النظام" : "System";
        ColCounted.Header = ar ? "المجرود" : "Counted";
        ColVariance.Header = ar ? "الفرق" : "Variance";

        Grid.ItemsSource = _lines;
    }

    private void OkClick(object sender, RoutedEventArgs e)
    {
        Grid.CommitEdit();
        var lang = LanguageManager.Instance;

        if (_lines.Any(l => l.CountedStock < 0))
        {
            CustomMessageBox.Show(lang.IsArabic ? "الكمية المجرودة لا يمكن أن تكون سالبة." : "Counted quantity cannot be negative.",
                lang.ValidationTitle, MsgType.Warning, this);
            return;
        }

        // Only the lines that actually changed need applying.
        Counts = _lines.Where(l => l.Variance != 0)
            .Select(l => (l.ProductId, l.CountedStock)).ToList();
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
