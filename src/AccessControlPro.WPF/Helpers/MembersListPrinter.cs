using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AccessControlPro.Application.DTOs;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Builds and prints an A4 report of the currently-loaded members list.
/// Header carries the gym identity (see <see cref="GymProfile"/>); columns are
/// #, name, card no, phone, subscription, expiry and status. Bilingual + RTL aware.
/// </summary>
public static class MembersListPrinter
{
    public static void Print(IReadOnlyList<EmployeeDto> members)
    {
        var lang = LanguageManager.Instance;
        bool ar = lang.IsArabic;

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return;

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(36),
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontSize = 11,
            ColumnWidth = double.PositiveInfinity, // single column
            FlowDirection = ar ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
        };

        // Gym header
        doc.Blocks.Add(new Paragraph(new Run(GymProfile.DisplayName))
        {
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        });
        var sub = ar ? "قائمة الأعضاء" : "Members List";
        doc.Blocks.Add(new Paragraph(new Run($"{sub}  —  {System.DateTime.Now:yyyy-MM-dd HH:mm}   ({members.Count})"))
        {
            FontSize = 11,
            Foreground = Brushes.Gray,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var table = new Table { CellSpacing = 0, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0) };
        // # / Name / Card / Phone / Subscription / Expiry / Status
        double[] widths = { 0.6, 3.2, 1.6, 1.8, 2.0, 1.4, 1.2 };
        foreach (var w in widths)
            table.Columns.Add(new TableColumn { Width = new GridLength(w, GridUnitType.Star) });

        var header = new TableRowGroup();
        var hr = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0x24, 0x7B, 0x7B)) };
        AddCell(hr, "#", true);
        AddCell(hr, ar ? "الاسم" : "Name", true);
        AddCell(hr, ar ? "البطاقة" : "Card No", true);
        AddCell(hr, ar ? "الهاتف" : "Phone", true);
        AddCell(hr, ar ? "الاشتراك" : "Subscription", true);
        AddCell(hr, ar ? "الانتهاء" : "Expiry", true);
        AddCell(hr, ar ? "الحالة" : "Status", true);
        header.Rows.Add(hr);
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        int i = 1;
        var today = System.DateTime.Today;
        foreach (var m in members)
        {
            var name = ar
                ? (string.IsNullOrWhiteSpace(m.FullNameAr) ? m.FullNameEn : m.FullNameAr)
                : (string.IsNullOrWhiteSpace(m.FullNameEn) ? m.FullNameAr : m.FullNameEn);

            string status = m.IsFrozen ? (ar ? "مجمّد" : "Frozen")
                : m.EndDate.Date < today ? (ar ? "منتهي" : "Expired")
                : (ar ? "نشط" : "Active");

            var row = new TableRow();
            if (i % 2 == 0) row.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF7, 0xF7));
            AddCell(row, i.ToString());
            AddCell(row, name);
            AddCell(row, m.CardNo);
            AddCell(row, m.Phone);
            AddCell(row, m.SubscriptionType);
            AddCell(row, m.EndDate.ToString("yyyy-MM-dd"));
            AddCell(row, status);
            body.Rows.Add(row);
            i++;
        }
        table.RowGroups.Add(body);
        doc.Blocks.Add(table);

        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        paginator.PageSize = new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
        printDialog.PrintDocument(paginator, "Members List");
    }

    private static void AddCell(TableRow row, string text, bool headerCell = false)
    {
        var para = new Paragraph(new Run(text ?? ""))
        {
            Margin = new Thickness(6, 4, 6, 4),
            FontWeight = headerCell ? FontWeights.Bold : FontWeights.Normal,
            Foreground = headerCell ? Brushes.White : Brushes.Black
        };
        row.Cells.Add(new TableCell(para)
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 0.5)
        });
    }
}
