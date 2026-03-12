using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class RenewalReceiptDialog : Window
{
    private readonly string _playerName;
    private readonly string _cardNo;
    private readonly string _subscriptionType;
    private readonly string _period;
    private readonly DateTime _startDate;
    private readonly DateTime _endDate;
    private readonly decimal _fee;
    private readonly decimal _paid;
    private readonly decimal _remaining;

    public RenewalReceiptDialog(
        string playerName, string cardNo, string subscriptionType,
        int months, int customDays,
        DateTime startDate, DateTime endDate,
        decimal fee, decimal paid)
    {
        _playerName = playerName;
        _cardNo = cardNo;
        _subscriptionType = subscriptionType;
        _startDate = startDate;
        _endDate = endDate;
        _fee = fee;
        _paid = paid;
        _remaining = fee - paid;

        var lang = LanguageManager.Instance;
        if (customDays > 0)
            _period = $"{customDays} {lang.RcpDays}";
        else
            _period = months == 1 ? $"1 {lang.RcpMonth}" : $"{months} {lang.RcpMonths}";

        InitializeComponent();

        DateText.Text = DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss");
        PlayerNameText.Text = playerName;
        CardNoText.Text = cardNo;
        SubTypeText.Text = subscriptionType;
        PeriodText.Text = _period;
        StartDateText.Text = startDate.ToString("yyyy-MM-dd");
        EndDateText.Text = endDate.ToString("yyyy-MM-dd");
        FeeText.Text = fee.ToString("N0");
        PaidText.Text = paid.ToString("N0");
        RemainingText.Text = _remaining.ToString("N0");
    }

    private void PrintClick(object sender, RoutedEventArgs e)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return;

        var doc = BuildPrintDocument();
        var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
        paginator.PageSize = new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
        printDialog.PrintDocument(paginator, "Renewal Receipt");
    }

    private FlowDocument BuildPrintDocument()
    {
        var lang = LanguageManager.Instance;
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontSize = 13,
            FlowDirection = lang.IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
        };

        // Title
        var title = new Paragraph(new Run(lang.RcpRenewalReceipt))
        {
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        doc.Blocks.Add(title);

        // Date
        var date = new Paragraph(new Run(DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss")))
        {
            FontSize = 11,
            Foreground = Brushes.Gray,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        };
        doc.Blocks.Add(date);

        // Details table
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var rg = new TableRowGroup();

        AddRow(rg, lang.RcpPlayerName, _playerName);
        AddRow(rg, lang.CardNo, _cardNo);
        AddRow(rg, lang.RcpSubscriptionType, _subscriptionType);
        AddRow(rg, lang.RcpPeriod, _period);
        AddRow(rg, lang.StartDate, _startDate.ToString("yyyy-MM-dd"));
        AddRow(rg, lang.EndDate, _endDate.ToString("yyyy-MM-dd"));

        table.RowGroups.Add(rg);
        doc.Blocks.Add(table);

        // Separator
        doc.Blocks.Add(new Paragraph(new Run("─────────────────────────────"))
        {
            TextAlignment = TextAlignment.Center,
            Foreground = Brushes.LightGray,
            Margin = new Thickness(0, 8, 0, 8)
        });

        // Financial details
        var finTable = new Table { CellSpacing = 0 };
        finTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        finTable.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var finRg = new TableRowGroup();

        AddRow(finRg, lang.Fee, _fee.ToString("N0"), FontWeights.Bold);
        AddRow(finRg, lang.Paid, _paid.ToString("N0"), FontWeights.Bold);
        AddRow(finRg, lang.Remaining, _remaining.ToString("N0"), FontWeights.Bold);

        finTable.RowGroups.Add(finRg);
        doc.Blocks.Add(finTable);

        return doc;
    }

    private static void AddRow(TableRowGroup rg, string label, string value, FontWeight? weight = null)
    {
        var row = new TableRow();
        var labelCell = new TableCell(new Paragraph(new Run(label))
        {
            FontWeight = weight ?? FontWeights.Normal,
            Margin = new Thickness(0, 3, 0, 3)
        });
        var valueCell = new TableCell(new Paragraph(new Run(value))
        {
            TextAlignment = TextAlignment.Right,
            FontWeight = weight ?? FontWeights.Normal,
            Margin = new Thickness(0, 3, 0, 3)
        });
        row.Cells.Add(labelCell);
        row.Cells.Add(valueCell);
        rg.Rows.Add(row);
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
