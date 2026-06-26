using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace AccessControlPro.WPF.Views;

/// <summary>
/// Full-screen blocking window shown when the gym is remotely locked (payment enforcement).
/// Re-checks the cloud every 60s and closes itself automatically once the admin unlocks.
/// Cannot be dismissed by the user while still locked.
/// </summary>
public partial class LockWindow : Window
{
    private readonly DispatcherTimer _timer;
    private bool _allowClose;

    public LockWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = string.IsNullOrWhiteSpace(message)
            ? "تم إيقاف البرنامج. يرجى التواصل مع المزود.\nThe software has been disabled. Please contact your provider."
            : message;
        ContactText.Text = LoadContact();
        try { MachineIdText.Text = new AccessControlPro.Application.Services.LicenseService().GetMachineId(); }
        catch { MachineIdText.Text = "—"; }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _timer.Tick += async (_, _) => await RecheckAsync(false);
        _timer.Start();

        Loaded += (_, _) => Activate();
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) e.Handled = true; };
    }

    public void UpdateMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message)) MessageText.Text = message;
    }

    private async void Recheck_Click(object sender, RoutedEventArgs e) => await RecheckAsync(true);

    private async void Unlock_Click(object sender, RoutedEventArgs e)
    {
        var code = UnlockCodeBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(code)) return;

        UnlockButton.IsEnabled = false;
        if (Helpers.RemoteLockService.TryOfflineUnlock(code))
        {
            StatusText.Text = "تم الفتح / Unlocked";
            await RecheckAsync(false); // re-evaluate — grace was reset, so it closes
        }
        else
        {
            StatusText.Text = "رمز غير صحيح / Invalid code";
        }
        UnlockButton.IsEnabled = true;
    }

    private async Task RecheckAsync(bool manual)
    {
        try
        {
            StatusText.Text = "جارٍ التحقق... / Checking...";
            RecheckButton.IsEnabled = false;
            var result = await Helpers.RemoteLockService.EvaluateAsync();
            if (!result.Locked)
            {
                _allowClose = true;
                _timer.Stop();
                Close();
                return;
            }
            UpdateMessage(result.Message);
            StatusText.Text = manual ? "لا يزال مقفلاً / Still locked" : "";
        }
        catch { }
        finally { RecheckButton.IsEnabled = true; }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose) { e.Cancel = true; return; } // can't be dismissed while still locked
        base.OnClosing(e);
    }

    private static string LoadContact()
    {
        try
        {
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(settingsPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (doc.RootElement.TryGetProperty("Developer", out var dev))
                {
                    var phone = dev.TryGetProperty("Phone", out var p) ? p.GetString() : "";
                    var wa = dev.TryGetProperty("WhatsApp", out var w) ? w.GetString() : "";
                    var contact = !string.IsNullOrWhiteSpace(wa) ? wa : phone;
                    if (!string.IsNullOrWhiteSpace(contact)) return $"\U0001F4DE {contact}";
                }
            }
        }
        catch { }
        return "";
    }
}
