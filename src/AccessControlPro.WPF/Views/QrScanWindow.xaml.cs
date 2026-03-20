using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using FontAwesome.WPF;

namespace AccessControlPro.WPF.Views;

public partial class QrScanWindow : Window
{
    private readonly IQrPassService _qrPassService;
    private readonly IDeviceService _deviceService;
    private string _scanBuffer = string.Empty;
    private DateTime _lastKeyTime = DateTime.MinValue;
    private int _scanCount;
    private bool _isProcessing;
    private readonly DispatcherTimer _resetTimer;

    public QrScanWindow(IQrPassService qrPassService, IDeviceService deviceService)
    {
        InitializeComponent();
        _qrPassService = qrPassService;
        _deviceService = deviceService;

        // Timer to reset status after showing result
        _resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _resetTimer.Tick += (_, _) =>
        {
            _resetTimer.Stop();
            ResetStatus();
        };

        Loaded += async (_, _) => await LoadDevicesAsync();
    }

    private async Task LoadDevicesAsync()
    {
        try
        {
            var devices = await _deviceService.GetAllDevicesAsync();
            DeviceCombo.ItemsSource = devices;
            if (devices.Any())
                DeviceCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QrScan] Failed to load devices: {ex.Message}");
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Barcode scanner sends characters very fast then Enter
        var now = DateTime.Now;

        if (e.Key == Key.Return || e.Key == Key.Enter)
        {
            if (_scanBuffer.Length > 0 && !_isProcessing)
            {
                e.Handled = true;
                var code = _scanBuffer;
                _scanBuffer = string.Empty;
                _ = ProcessScanAsync(code);
            }
            return;
        }

        // If too much time passed since last key, start fresh buffer
        if ((now - _lastKeyTime).TotalMilliseconds > 100)
            _scanBuffer = string.Empty;

        _lastKeyTime = now;

        // Convert key to character
        var ch = KeyToChar(e.Key);
        if (ch.HasValue)
        {
            _scanBuffer += ch.Value;
            e.Handled = true;
        }
    }

    private async Task ProcessScanAsync(string passCode)
    {
        if (_isProcessing) return;
        _isProcessing = true;
        try
        {
            ShowProcessing();

            var (isValid, message, pass) = await _qrPassService.ValidateAndUseAsync(passCode);

            if (isValid && pass != null)
            {
                _scanCount++;
                ScanCountText.Text = $"Scans: {_scanCount}";

                // Open the device/door assigned to this pass
                var deviceId = pass.DeviceId;
                var doorNumber = pass.DoorNumber > 0 ? pass.DoorNumber : 1;

                // Fallback to manually selected device if pass has no device assigned
                if (deviceId == null)
                {
                    var selectedDevice = DeviceCombo.SelectedItem as DeviceDto;
                    deviceId = selectedDevice?.Id;
                }

                if (deviceId != null)
                {
                    try
                    {
                        await _deviceService.RemoteOpenDoorAsync(deviceId.Value, doorNumber);
                    }
                    catch (Exception doorEx)
                    {
                        Debug.WriteLine($"[QrScan] Door open failed: {doorEx.Message}");
                    }
                }

                ShowSuccess(message, pass);
            }
            else if (isValid)
            {
                _scanCount++;
                ScanCountText.Text = $"Scans: {_scanCount}";
                ShowSuccess(message, pass);
            }
            else
            {
                ShowError(message, pass);
            }

            _resetTimer.Start();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message, null);
            _resetTimer.Start();
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void ShowProcessing()
    {
        StatusIcon.Icon = FontAwesomeIcon.Spinner;
        StatusIcon.Spin = true;
        StatusIcon.Foreground = (Brush)FindResource("PrimaryBrush");
        StatusText.Text = "Processing...";
        DetailText.Text = "";
    }

    private void ShowSuccess(string message, QrPassDto? pass)
    {
        StatusIcon.Icon = FontAwesomeIcon.CheckCircle;
        StatusIcon.Spin = false;
        StatusIcon.Foreground = (Brush)FindResource("SuccessBrush");
        StatusText.Text = pass?.PlayerName ?? "Access Granted";
        DetailText.Text = message;
    }

    private void ShowError(string message, QrPassDto? pass)
    {
        StatusIcon.Icon = FontAwesomeIcon.TimesCircle;
        StatusIcon.Spin = false;
        StatusIcon.Foreground = (Brush)FindResource("ErrorBrush");
        StatusText.Text = pass?.PlayerName ?? "Access Denied";
        DetailText.Text = message;
    }

    private void ResetStatus()
    {
        StatusIcon.Icon = FontAwesomeIcon.Qrcode;
        StatusIcon.Spin = false;
        StatusIcon.Foreground = (Brush)FindResource("PrimaryBrush");
        StatusText.Text = "Ready to scan";
        DetailText.Text = "Scan a QR code to validate access";
    }

    private static char? KeyToChar(Key key)
    {
        // Handle alphanumeric keys that barcode scanners send
        if (key >= Key.A && key <= Key.Z)
            return (char)('A' + (key - Key.A));
        if (key >= Key.D0 && key <= Key.D9)
            return (char)('0' + (key - Key.D0));
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return (char)('0' + (key - Key.NumPad0));
        return null;
    }

    protected override void OnClosed(EventArgs e)
    {
        _resetTimer.Stop();
        base.OnClosed(e);
    }

    private void CloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
