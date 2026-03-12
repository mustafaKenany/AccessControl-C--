using System.Windows;

namespace AccessControlPro.WPF.Views;

public partial class DownloadPeriodDialog : Window
{
    /// <summary>
    /// Number of months the user selected (1, 3, 6, or 12).
    /// </summary>
    public int SelectedMonths { get; private set; } = 1;

    public DownloadPeriodDialog()
    {
        InitializeComponent();
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (Opt1Month.IsChecked == true) SelectedMonths = 1;
        else if (Opt3Months.IsChecked == true) SelectedMonths = 3;
        else if (Opt6Months.IsChecked == true) SelectedMonths = 6;
        else if (Opt1Year.IsChecked == true) SelectedMonths = 12;

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
