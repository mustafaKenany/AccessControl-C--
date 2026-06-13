using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Builds and prints an 80mm thermal subscription receipt (gym header + member/
/// subscription details + fee/paid/remaining). Used for new-player registration;
/// the same 80mm layout the POS and renewal receipts use.
/// </summary>
public static class ThermalReceipt
{
    private const double Width80mm = 302; // 80mm roll at 96 DPI

    public static void PrintSubscription(
        string title, string playerName, string cardNo, string subscriptionType,
        DateTime startDate, DateTime endDate, decimal fee, decimal paid)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return;

        var lang = LanguageManager.Instance;
        var doc = new FlowDocument
        {
            PageWidth = Width80mm,
            ColumnWidth = Width80mm,
            PagePadding = new Thickness(10),
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontSize = 11,
            FlowDirection = lang.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
        };

        Centered(doc, GymProfile.DisplayName, 16, FontWeights.Bold, 2);
        if (!string.IsNullOrWhiteSpace(GymProfile.Phone))
            Centered(doc, GymProfile.Phone, 9, FontWeights.Normal, 4, Brushes.Gray);
        Centered(doc, title, 13, FontWeights.Bold, 4);
        Centered(doc, DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss"), 9, FontWeights.Normal, 8, Brushes.Gray);

        var table = TwoColTable();
        var rg = new TableRowGroup();
        AddRow(rg, lang.RcpPlayerName, playerName);
        AddRow(rg, lang.CardNo, cardNo);
        AddRow(rg, lang.RcpSubscriptionType, subscriptionType);
        AddRow(rg, lang.StartDate, startDate.ToString("yyyy-MM-dd"));
        AddRow(rg, lang.EndDate, endDate.ToString("yyyy-MM-dd"));
        table.RowGroups.Add(rg);
        doc.Blocks.Add(table);

        doc.Blocks.Add(new Paragraph(new Run(new string('-', 34)))
        {
            TextAlignment = TextAlignment.Center,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 6, 0, 6)
        });

        var finTable = TwoColTable();
        var finRg = new TableRowGroup();
        AddRow(finRg, lang.Fee, fee.ToString("N0"), FontWeights.Bold);
        AddRow(finRg, lang.Paid, paid.ToString("N0"), FontWeights.Bold);
        AddRow(finRg, lang.Remaining, (fee - paid).ToString("N0"), FontWeights.Bold);
        finTable.RowGroups.Add(finRg);
        doc.Blocks.Add(finTable);

        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        printDialog.PrintDocument(paginator, title);
    }

    private static void Centered(FlowDocument doc, string text, double size, FontWeight weight,
        double bottomMargin, Brush? brush = null)
    {
        doc.Blocks.Add(new Paragraph(new Run(text ?? ""))
        {
            FontSize = size,
            FontWeight = weight,
            Foreground = brush ?? Brushes.Black,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, bottomMargin)
        });
    }

    private static Table TwoColTable()
    {
        var t = new Table { CellSpacing = 0 };
        t.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        t.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        return t;
    }

    private static void AddRow(TableRowGroup rg, string label, string value, FontWeight? weight = null)
    {
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(new Run(label))
        {
            FontWeight = weight ?? FontWeights.Normal,
            Margin = new Thickness(0, 2, 0, 2)
        }));
        row.Cells.Add(new TableCell(new Paragraph(new Run(value))
        {
            TextAlignment = TextAlignment.Right,
            FontWeight = weight ?? FontWeights.Normal,
            Margin = new Thickness(0, 2, 0, 2)
        }));
        rg.Rows.Add(row);
    }
}
