using System;
using System.IO;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AccessControlPro.WPF.Views;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Builds and prints an 80mm thermal receipt (gym logo + name + owner header, details,
/// totals, a running receipt number). Prints DIRECTLY to the default Windows printer —
/// no printer-picker dialog — and shows a friendly error if no printer is connected.
/// Used for new-player registration, renewals, and income/expense cash entries.
/// </summary>
public static class ThermalReceipt
{
    // Content width tuned to the printable area of an 80mm roll (~72mm) so right-aligned
    // values (dates, amounts) don't get clipped at the paper edge.
    private const double Width80mm = 270;

    public static void PrintSubscription(
        string title, string playerName, string cardNo, string subscriptionType,
        DateTime startDate, DateTime endDate, decimal fee, decimal paid)
    {
        var lang = LanguageManager.Instance;
        var doc = NewDoc(lang);

        BuildHeader(doc, title);

        var table = TwoColTable();
        var rg = new TableRowGroup();
        AddRow(rg, lang.RcpPlayerName, playerName);
        AddRow(rg, lang.CardNo, cardNo);
        AddRow(rg, lang.RcpSubscriptionType, subscriptionType);
        AddRow(rg, lang.StartDate, startDate.ToString("yyyy-MM-dd"));
        AddRow(rg, lang.EndDate, endDate.ToString("yyyy-MM-dd"));
        table.RowGroups.Add(rg);
        doc.Blocks.Add(table);

        Divider(doc);

        var finTable = TwoColTable();
        var finRg = new TableRowGroup();
        AddRow(finRg, lang.Fee, fee.ToString("N0"), FontWeights.Bold);
        AddRow(finRg, lang.Paid, paid.ToString("N0"), FontWeights.Bold);
        AddRow(finRg, lang.Remaining, (fee - paid).ToString("N0"), FontWeights.Bold);
        finTable.RowGroups.Add(finRg);
        doc.Blocks.Add(finTable);

        PrintToDefault(doc, title);
    }

    /// <summary>
    /// 80mm receipt for a one-off cash entry (income "وارد" or expense): header, title,
    /// category, description, and amount. Used by the Cash Flow add-income/expense flow.
    /// </summary>
    public static void PrintCashReceipt(string title, string category, string description, decimal amount)
    {
        var lang = LanguageManager.Instance;
        var doc = NewDoc(lang);

        BuildHeader(doc, title);

        var table = TwoColTable();
        var rg = new TableRowGroup();
        if (!string.IsNullOrWhiteSpace(category))
            AddRow(rg, lang.IsArabic ? "التصنيف" : "Category", category);
        if (!string.IsNullOrWhiteSpace(description))
            AddRow(rg, lang.IsArabic ? "التفاصيل" : "Details", description);
        table.RowGroups.Add(rg);
        doc.Blocks.Add(table);

        Divider(doc);

        var amtTable = TwoColTable();
        var amtRg = new TableRowGroup();
        AddRow(amtRg, lang.IsArabic ? "المبلغ" : "Amount", amount.ToString("N0"), FontWeights.Bold);
        amtTable.RowGroups.Add(amtRg);
        doc.Blocks.Add(amtTable);

        Centered(doc, lang.IsArabic ? "شكراً" : "Thank you", 11, FontWeights.Bold, 0);

        PrintToDefault(doc, title);
    }

    /// <summary>
    /// 80mm receipt for an anonymous daily-entry pass (temp card / bracelet): gym header,
    /// the bracelet/card number, valid-from & valid-to dates, and the price. NO member name —
    /// a daily pass is anonymous. Prints directly to the default printer.
    /// </summary>
    public static void PrintDailyPass(string cardNo, DateTime validFrom, DateTime validTo, decimal fee)
    {
        var lang = LanguageManager.Instance;
        var doc = NewDoc(lang);
        var title = lang.IsArabic ? "دخول يومي" : "Daily Pass";

        BuildHeader(doc, title);

        var table = TwoColTable();
        var rg = new TableRowGroup();
        AddRow(rg, lang.IsArabic ? "رقم السوار" : "Bracelet No.", cardNo);
        AddRow(rg, lang.StartDate, validFrom.ToString("yyyy-MM-dd"));
        AddRow(rg, lang.EndDate, validTo.ToString("yyyy-MM-dd"));
        table.RowGroups.Add(rg);
        doc.Blocks.Add(table);

        Divider(doc);

        var finTable = TwoColTable();
        var finRg = new TableRowGroup();
        AddRow(finRg, lang.Fee, fee.ToString("N0"), FontWeights.Bold);
        finTable.RowGroups.Add(finRg);
        doc.Blocks.Add(finTable);

        Centered(doc, lang.IsArabic ? "شكراً" : "Thank you", 11, FontWeights.Bold, 0);

        PrintToDefault(doc, title);
    }

    // ---- shared building blocks ----

    private static FlowDocument NewDoc(LanguageManager lang) => new()
    {
        PageWidth = Width80mm,
        ColumnWidth = Width80mm,
        PagePadding = new Thickness(6, 8, 6, 8),
        FontFamily = new FontFamily("Segoe UI, Arial"),
        FontSize = 11,
        FlowDirection = lang.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
    };

    /// <summary>Gym logo (if set) + name + owner + phone + title + receipt number / time.
    /// Public so the renewal dialog reuses the exact same header.</summary>
    public static void BuildHeader(FlowDocument doc, string title)
    {
        var lang = LanguageManager.Instance;

        bool logoDrawn = false;
        if (!string.IsNullOrWhiteSpace(GymProfile.LogoPath) && File.Exists(GymProfile.LogoPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(GymProfile.LogoPath, UriKind.Absolute);
                bmp.EndInit();
                var img = new Image
                {
                    Source = bmp,
                    Width = 72,
                    Height = 72,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                doc.Blocks.Add(new BlockUIContainer(img) { Margin = new Thickness(0, 0, 0, 4) });
                logoDrawn = true;
            }
            catch { /* a bad logo path must never block the receipt */ }
        }
        if (!logoDrawn)
        {
            // Reserve a blank area at the very top as the logo's place, so the receipt always has a
            // spot for it. Upload a logo once in Admin → Settings and it fills this space automatically.
            doc.Blocks.Add(new BlockUIContainer(new Border { Height = 44 }) { Margin = new Thickness(0, 0, 0, 4) });
        }

        Centered(doc, GymProfile.DisplayName, 16, FontWeights.Bold, 2);
        if (!string.IsNullOrWhiteSpace(GymProfile.Owner))
            Centered(doc, GymProfile.Owner, 10, FontWeights.Normal, 2, Brushes.DimGray);
        if (!string.IsNullOrWhiteSpace(GymProfile.Phone))
        {
            // Print the phone as a plain left-to-right string with no spaces. On an Arabic (RTL)
            // receipt a raw number gets visually regrouped (e.g. "7375 917 0772"); forcing LTR and
            // stripping whitespace prints it exactly as entered, e.g. 07712345678.
            var phone = new string(GymProfile.Phone.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
            doc.Blocks.Add(new Paragraph(new Run(phone))
            {
                FontSize = 9,
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Thickness(0, 0, 0, 4)
            });
        }
        Centered(doc, title, 13, FontWeights.Bold, 2);

        var serialLabel = lang.IsArabic ? "رقم الوصل" : "Receipt #";
        Centered(doc, $"{serialLabel}: {NextSerial()}    {DateTime.Now:yyyy-MM-dd  HH:mm}",
            9, FontWeights.Normal, 8, Brushes.Gray);
    }

    /// <summary>Persistent, incrementing receipt number stored per PC (5-digit, e.g. 00042).</summary>
    private static string NextSerial()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AccessControlPro");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "receipt_counter.txt");
            long n = 0;
            if (File.Exists(file) && long.TryParse(File.ReadAllText(file).Trim(), out var cur)) n = cur;
            n++;
            File.WriteAllText(file, n.ToString());
            return n.ToString("D5");
        }
        catch { return DateTime.Now.ToString("HHmmss"); }
    }

    /// <summary>Prints a document to the default printer with no dialog; warns if none is connected.
    /// Public so the renewal dialog (which builds its own document) reuses it.</summary>
    public static void PrintToDefault(FlowDocument doc, string jobName)
    {
        var pd = DefaultPrinterOrWarn();
        if (pd == null) return;
        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        pd.PrintDocument(paginator, jobName);
    }

    /// <summary>Returns a PrintDialog pointed at the default printer, or null (after showing a
    /// friendly "no printer" message) if none is connected. Used for direct, no-dialog printing.</summary>
    public static PrintDialog? DefaultPrinterOrWarn()
    {
        PrintQueue? dq = null;
        try { dq = new LocalPrintServer().DefaultPrintQueue; }
        catch { /* no print server / no printers */ }

        if (dq == null)
        {
            var ar = LanguageManager.Instance.IsArabic;
            CustomMessageBox.Show(
                ar ? "لا توجد طابعة متصلة. الرجاء توصيل طابعة الوصولات وتعيينها كطابعة افتراضية."
                   : "No printer found. Please connect the receipt printer and set it as the default printer.",
                ar ? "طباعة" : "Print", MsgType.Warning);
            return null;
        }
        return new PrintDialog { PrintQueue = dq };
    }

    private static void Divider(FlowDocument doc)
    {
        doc.Blocks.Add(new Paragraph(new Run(new string('-', 32)))
        {
            TextAlignment = TextAlignment.Center,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 6, 0, 6)
        });
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
        })
        { Padding = new Thickness(2, 0, 2, 0) });
        row.Cells.Add(new TableCell(new Paragraph(new Run(value))
        {
            TextAlignment = TextAlignment.Right,
            FontWeight = weight ?? FontWeights.Normal,
            Margin = new Thickness(0, 2, 0, 2)
        })
        { Padding = new Thickness(2, 0, 2, 0) });
        rg.Rows.Add(row);
    }
}
