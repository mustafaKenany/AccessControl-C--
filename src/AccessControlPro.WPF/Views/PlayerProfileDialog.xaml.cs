using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class PlayerProfileDialog : Window
{
    private readonly IEmployeeService _employeeService;
    private readonly int _employeeId;

    public PlayerProfileDialog(IEmployeeService employeeService, int employeeId)
    {
        _employeeService = employeeService;
        _employeeId = employeeId;
        InitializeComponent();
        Loaded += async (_, _) => await LoadProfileAsync();
    }

    private async Task LoadProfileAsync()
    {
        var lang = LanguageManager.Instance;
        try
        {
            var profile = await _employeeService.GetPlayerProfileAsync(_employeeId);
            var p = profile.Player;

            // Header
            PlayerNameText.Text = lang.IsArabic ? p.FullNameAr : p.FullNameEn;
            CardNoText.Text = p.CardNo;
            SubTypeText.Text = p.SubscriptionType;

            if (p.IsFrozen)
            {
                StatusText.Text = lang.RptFrozen;
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x78, 0xCD, 0xD7));
            }
            else if (p.EndDate < DateTime.Today)
            {
                StatusText.Text = lang.RptExpired;
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF7, 0x68, 0x5B));
            }
            else
            {
                StatusText.Text = lang.FilterActive;
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0xD4, 0x7A));
            }

            // Photo display
            if (p.PhotoData != null && p.PhotoData.Length > 0)
            {
                var source = BytesToBitmapImage(p.PhotoData);
                if (source != null)
                {
                    PlayerPhoto.Source = source;
                    PlayerPhoto.Visibility = Visibility.Visible;
                    DefaultIcon.Visibility = Visibility.Collapsed;

                    OverviewPhoto.Source = source;
                    OverviewPhoto.Visibility = Visibility.Visible;
                    NoPhotoContainer.Visibility = Visibility.Collapsed;
                }
            }

            // Overview
            PhoneLabel.Text = lang.Phone;
            PhoneValue.Text = p.Phone;
            StartLabel.Text = lang.StartDate;
            StartValue.Text = p.StartDate.ToString("yyyy-MM-dd");
            EndLabel.Text = lang.EndDate;
            EndValue.Text = p.EndDate.ToString("yyyy-MM-dd");
            HeightLabel.Text = lang.HeightCm;
            HeightValue.Text = p.Height > 0 ? $"{p.Height}" : "—";
            FeeLabel.Text = lang.Fee;
            FeeValue.Text = p.SubscriptionFee.ToString("N0");
            PaidLabel.Text = lang.Paid;
            PaidValue.Text = p.AmountPaid.ToString("N0");
            RemLabel.Text = lang.Remaining;
            RemValue.Text = p.RemainingBalance.ToString("N0");
            WeightLabel.Text = lang.WeightKg;
            WeightValue.Text = p.Weight > 0 ? $"{p.Weight}" : "—";

            // Freeze history
            if (profile.FreezeHistory.Count > 0)
                FreezeList.ItemsSource = profile.FreezeHistory;
            else
            {
                NoFreezeText.Text = lang.PrfNoRecords;
                NoFreezeText.Visibility = Visibility.Visible;
            }

            // Transactions
            if (profile.Transactions.Count > 0)
                TransactionList.ItemsSource = profile.Transactions;
            else
            {
                NoTransText.Text = lang.PrfNoRecords;
                NoTransText.Visibility = Visibility.Visible;
            }

            // Audit logs - set IsArabic for display
            foreach (var log in profile.AuditLogs)
                log.IsArabic = lang.IsArabic;

            if (profile.AuditLogs.Count > 0)
                AuditList.ItemsSource = profile.AuditLogs;
            else
            {
                NoLogsText.Text = lang.PrfNoRecords;
                NoLogsText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, lang.ValidationTitle, MsgType.Error, this);
            Close();
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private BitmapImage? BytesToBitmapImage(byte[] imageData)
    {
        try
        {
            var bitImage = new BitmapImage();
            using (var ms = new System.IO.MemoryStream(imageData))
            {
                bitImage.BeginInit();
                bitImage.StreamSource = ms;
                bitImage.CacheOption = BitmapCacheOption.OnLoad;
                bitImage.EndInit();
            }
            bitImage.Freeze();
            return bitImage;
        }
        catch
        {
            return null;
        }
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }
}

