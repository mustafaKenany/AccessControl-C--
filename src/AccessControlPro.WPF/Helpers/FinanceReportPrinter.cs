using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Domain.Enums;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Prints an A4 income or expense report for the current Finance filter:
/// gym header, period, a table (date / category / description / member / payment /
/// amount) and the filtered total. Bilingual + RTL aware.
/// </summary>
public static class FinanceReportPrinter
{
    public static void Print(TransactionType type, IReadOnlyList<TransactionDto> items, string periodText)
    {
        var lang = LanguageManager.Instance;
        bool ar = lang.IsArabic;

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return;

        bool income = type == TransactionType.Income;
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(36),
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontSize = 11,
            ColumnWidth = double.PositiveInfinity,
            FlowDirection = ar ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
        };

        doc.Blocks.Add(new Paragraph(new Run(GymProfile.DisplayName))
        {
            FontSize = 20, FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 2)
        });
        var title = income ? (ar ? "تقرير الواردات" : "Income Report")
                           : (ar ? "تقرير المصروفات" : "Expense Report");
        var range = string.IsNullOrWhiteSpace(periodText) ? (ar ? "كل الفترات" : "All time") : periodText;
        doc.Blocks.Add(new Paragraph(new Run($"{title}  —  {range}   ({items.Count})"))
        {
            FontSize = 11, Foreground = Brushes.Gray,
            TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 12)
        });

        var table = new Table { CellSpacing = 0 };
        // Date / Category / Description / Member / Payment / Amount
        double[] widths = { 1.6, 1.6, 3.0, 2.0, 1.1, 1.4 };
        foreach (var w in widths)
            table.Columns.Add(new TableColumn { Width = new GridLength(w, GridUnitType.Star) });

        var accent = income ? Color.FromRgb(0x1E, 0x7E, 0x4F) : Color.FromRgb(0xB0, 0x3A, 0x2E);
        var hg = new TableRowGroup();
        var hr = new TableRow { Background = new SolidColorBrush(accent) };
        AddCell(hr, ar ? "التاريخ" : "Date", true);
        AddCell(hr, ar ? "التصنيف" : "Category", true);
        AddCell(hr, ar ? "الوصف" : "Description", true);
        AddCell(hr, ar ? "العضو" : "Member", true);
        AddCell(hr, ar ? "الدفع" : "Payment", true);
        AddCell(hr, ar ? "المبلغ" : "Amount", true, TextAlignment.Right);
        hg.Rows.Add(hr);
        table.RowGroups.Add(hg);

        var bg = new TableRowGroup();
        int i = 0;
        decimal total = 0;
        foreach (var t in items)
        {
            total += t.Amount;
            var row = new TableRow();
            if (i++ % 2 == 1) row.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF7, 0xF7));
            AddCell(row, t.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
            AddCell(row, t.Category);
            AddCell(row, t.Description);
            AddCell(row, t.RelatedEmployeeName ?? "");
            AddCell(row, t.PaymentMethod == PaymentMethod.Cash ? (ar ? "نقدي" : "Cash") : (ar ? "بطاقة" : "Card"));
            AddCell(row, t.Amount.ToString("N0"), false, TextAlignment.Right);
            bg.Rows.Add(row);
        }
        // Total row
        var totalRow = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xEF, 0xEF)) };
        AddCell(totalRow, ar ? "الإجمالي" : "TOTAL", true, TextAlignment.Left, Brushes.Black);
        AddCell(totalRow, "");
        AddCell(totalRow, "");
        AddCell(totalRow, "");
        AddCell(totalRow, "");
        AddCell(totalRow, total.ToString("N0"), true, TextAlignment.Right, Brushes.Black);
        bg.Rows.Add(totalRow);
        table.RowGroups.Add(bg);

        doc.Blocks.Add(table);

        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        paginator.PageSize = new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
        printDialog.PrintDocument(paginator, income ? "Income Report" : "Expense Report");
    }

    private static void AddCell(TableRow row, string text, bool headerCell = false,
        TextAlignment align = TextAlignment.Left, Brush? overrideBrush = null)
    {
        var para = new Paragraph(new Run(text ?? ""))
        {
            Margin = new Thickness(6, 4, 6, 4),
            TextAlignment = align,
            FontWeight = headerCell ? FontWeights.Bold : FontWeights.Normal,
            Foreground = overrideBrush ?? (headerCell ? Brushes.White : Brushes.Black)
        };
        row.Cells.Add(new TableCell(para)
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 0.5)
        });
    }
}
