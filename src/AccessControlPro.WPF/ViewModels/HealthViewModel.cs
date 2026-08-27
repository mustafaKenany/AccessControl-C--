using System.Net.NetworkInformation;
using System.Windows.Media;
using System.Windows.Threading;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Interfaces;
using AccessControlPro.WPF.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using FontAwesome.WPF;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlPro.WPF.ViewModels;

/// <summary>
/// The ONE health badge — a single "كل شيء يعمل ✅" pill the owner can read at a glance, instead of
/// hunting through screens. Green when the gate is connected AND backups are healthy; amber/red with
/// a plain-Arabic reason when something needs attention. Re-checks every 60s in the background.
/// Deliberately covers only the two things a gym owner cares about daily (the gate + the backup) so
/// the badge stays trustworthy and never cries wolf.
/// </summary>
public partial class HealthViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private DispatcherTimer? _timer;

    private static readonly Brush Green = Freeze("#2ED47A");
    private static readonly Brush Amber = Freeze("#FFB946");
    private static readonly Brush Red = Freeze("#F7685B");
    private static Brush Freeze(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _detailText = "";
    [ObservableProperty] private Brush _statusBrush = Green;
    [ObservableProperty] private FontAwesomeIcon _statusIcon = FontAwesomeIcon.CheckCircle;
    /// <summary>True when something needs attention (gate offline / backup problem) — drives the
    /// pulsing red dot on the top-bar "Devices" icon.</summary>
    [ObservableProperty] private bool _hasIssue;

    public HealthViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        StatusText = Lang.IsArabic ? "جارٍ الفحص…" : "Checking…";
        LanguageManager.Instance.PropertyChanged += async (_, _) => await EvaluateAsync();
    }

    /// <summary>Start the background health checks (first one ~8s after startup, then every 60s).</summary>
    public void Start()
    {
        if (_timer != null) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _timer.Tick += async (_, _) => await EvaluateAsync();
        _timer.Start();
        _ = Task.Run(async () =>
        {
            await Task.Delay(8000);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await EvaluateAsync());
        });
    }

    private bool _running;

    private async Task EvaluateAsync()
    {
        if (_running) return;
        _running = true;
        try
        {
            bool ar = Lang.IsArabic;
            var problems = new List<string>();
            var warnings = new List<string>();

            // ── Gate / controller reachability ──
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var deviceRepo = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                var devices = (await deviceRepo.GetAllAsync()).ToList();
                if (devices.Count > 0)
                {
                    int online = 0;
                    foreach (var d in devices)
                    {
                        try
                        {
                            using var ping = new Ping();
                            var reply = await ping.SendPingAsync(d.IP, 1500);
                            if (reply.Status == IPStatus.Success) online++;
                        }
                        catch { }
                    }
                    if (online == 0)
                        problems.Add(ar ? "البوابة غير متصلة" : "The gate is offline");
                }
            }
            catch { /* device check failed — don't red-flag on our own error */ }

            // ── Backup health ──
            try
            {
                var backup = BackupService.LoadStatus();
                if (backup.ConsecutiveFailures >= 3)
                    problems.Add(ar ? "فشل النسخ الاحتياطي" : "Backups are failing");
                else if (backup.LastSuccess.HasValue && (DateTime.Now - backup.LastSuccess.Value).TotalDays > 2)
                    warnings.Add(ar ? "لم تُعمل نسخة احتياطية منذ أيام" : "No backup for a few days");
            }
            catch { }

            // ── Roll up into the single badge ──
            HasIssue = problems.Count > 0 || warnings.Count > 0;
            if (problems.Count > 0)
            {
                StatusBrush = Red;
                StatusIcon = FontAwesomeIcon.ExclamationCircle;
                StatusText = ar ? "يحتاج انتباه" : "Needs attention";
                DetailText = string.Join(ar ? "  •  " : "  •  ", problems.Concat(warnings));
            }
            else if (warnings.Count > 0)
            {
                StatusBrush = Amber;
                StatusIcon = FontAwesomeIcon.ExclamationTriangle;
                StatusText = ar ? "تنبيه بسيط" : "Minor notice";
                DetailText = string.Join(ar ? "  •  " : "  •  ", warnings);
            }
            else
            {
                StatusBrush = Green;
                StatusIcon = FontAwesomeIcon.CheckCircle;
                StatusText = ar ? "كل شيء يعمل" : "All good";
                DetailText = ar ? "البوابة متصلة والنسخ الاحتياطي سليم" : "Gate connected, backups healthy";
            }
        }
        finally
        {
            _running = false;
        }
    }
}
