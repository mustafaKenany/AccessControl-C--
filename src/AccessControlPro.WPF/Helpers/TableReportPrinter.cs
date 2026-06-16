using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Generic A4 table report printer: gym header, title + period, a bordered table, and an
/// optional total row. Opens the system PrintDialog — the user can pick a real printer or
/// "Microsoft Print to PDF" to save a PDF. Bilingual + RTL aware. Reused by the Stock
/// Movements view and the Reports section so every printout looks the same.
/// </summary>
public static class TableReportPrinter
{
    public static void Print(
        string title,
        string periodText,
        IReadOnlyList<string> headers,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<int>? rightAlignColumns = null,
        IReadOnlyList<string>? totalRow = null,
        string? docName = null)
    {
        var lang = LanguageManager.Instance;
        bool ar = lang.IsArabic;

        var printDialog = new System.Windows.Controls.PrintDialog();
        if (printDialog.ShowDialog() != true) return;

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
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2)
        });

        var range = string.IsNullOrWhiteSpace(periodText) ? (ar ? "كل الفترات" : "All time") : periodText;
        doc.Blocks.Add(new Paragraph(new Run($"{title}  —  {range}   ({rows.Count})"))
        {
            FontSize = 11,
            Foreground = Brushes.Gray,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        });

        var table = new Table { CellSpacing = 0 };
        foreach (var w in columnWidths)
            table.Columns.Add(new TableColumn { Width = new GridLength(w, GridUnitType.Star) });

        bool IsRight(int col) => rightAlignColumns != null && rightAlignColumns.Contains(col);

        var accent = Color.FromRgb(0x14, 0x34, 0x36); // matches the app's header teal
        var headerGroup = new TableRowGroup();
        var headerRow = new TableRow { Background = new SolidColorBrush(accent) };
        for (int c = 0; c < headers.Count; c++)
            AddCell(headerRow, headers[c], true, IsRight(c) ? TextAlignment.Right : TextAlignment.Left);
        headerGroup.Rows.Add(headerRow);
        table.RowGroups.Add(headerGroup);

        var bodyGroup = new TableRowGroup();
        int i = 0;
        foreach (var r in rows)
        {
            var row = new TableRow();
            if (i++ % 2 == 1) row.Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF7, 0xF7));
            for (int c = 0; c < headers.Count; c++)
            {
                var text = c < r.Count ? r[c] : "";
                AddCell(row, text, false, IsRight(c) ? TextAlignment.Right : TextAlignment.Left);
            }
            bodyGroup.Rows.Add(row);
        }

        if (totalRow != null)
        {
            var tr = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xEF, 0xEF)) };
            for (int c = 0; c < headers.Count; c++)
            {
                var text = c < totalRow.Count ? totalRow[c] : "";
                AddCell(tr, text, true, IsRight(c) ? TextAlignment.Right : TextAlignment.Left, Brushes.Black);
            }
            bodyGroup.Rows.Add(tr);
        }
        table.RowGroups.Add(bodyGroup);

        doc.Blocks.Add(table);

        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        paginator.PageSize = new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
        printDialog.PrintDocument(paginator, docName ?? title);
    }

    private static void AddCell(TableRow row, string text, bool headerCell,
        TextAlignment align, Brush? overrideBrush = null)
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
