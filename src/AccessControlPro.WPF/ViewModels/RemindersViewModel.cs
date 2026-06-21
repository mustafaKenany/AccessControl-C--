using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AccessControlPro.Application.DTOs;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.WPF.ViewModels;

/// <summary>
/// Renewal-reminder workflow: lists members whose subscription is expiring soon (or already
/// expired) and lets the cashier send each a WhatsApp reminder in one click, using an editable
/// message template. No paid SMS/WhatsApp gateway — it opens wa.me with the message pre-filled.
/// </summary>
public partial class RemindersViewModel : ObservableObject
{
    private readonly IEmployeeService _employeeService;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _daysAhead = 7;
    [ObservableProperty] private bool _includeExpired = true;
    [ObservableProperty] private string _template = WhatsAppHelper.LoadTemplate();

    public ObservableCollection<ReminderRow> Members { get; } = new();

    public RemindersViewModel(IEmployeeService employeeService) => _employeeService = employeeService;

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var today = DateTime.Today;
            var list = (await _employeeService.GetExpiringAsync(today, today.AddDays(Math.Max(0, DaysAhead)))).ToList();
            if (IncludeExpired)
                list.AddRange(await _employeeService.GetExpiredPlayersAsync());

            Members.Clear();
            foreach (var e in list.Where(e => !string.IsNullOrWhiteSpace(e.Phone))
                                  .GroupBy(e => e.Id).Select(g => g.First())   // de-dup
                                  .OrderBy(e => e.EndDate))
                Members.Add(new ReminderRow(e, (e.EndDate.Date - today).Days));
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally { IsLoading = false; }
    }

    partial void OnDaysAheadChanged(int value) => _ = LoadAsync();
    partial void OnIncludeExpiredChanged(bool value) => _ = LoadAsync();
    partial void OnTemplateChanged(string value) => WhatsAppHelper.SaveTemplate(value);

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    [RelayCommand]
    private void SendWhatsApp(ReminderRow? row)
    {
        if (row == null) return;
        var msg = WhatsAppHelper.BuildMessage(Template, row.Name, row.EndDate, row.DaysLeft);
        if (!WhatsAppHelper.OpenChat(row.Phone, msg))
            CustomMessageBox.Show(
                Lang.IsArabic ? "رقم الهاتف غير صالح للواتساب" : "This phone number isn't valid for WhatsApp",
                Lang.ValidationTitle, MsgType.Warning);
    }
}

/// <summary>One row in the reminders list — wraps a member with the computed days-left + status.</summary>
public class ReminderRow
{
    private readonly EmployeeDto _e;
    public ReminderRow(EmployeeDto e, int daysLeft) { _e = e; DaysLeft = daysLeft; }

    public int DaysLeft { get; }
    public string Phone => _e.Phone;
    public DateTime EndDate => _e.EndDate;
    public string SubscriptionType => _e.SubscriptionType;
    public string Name => LanguageManager.Instance.IsArabic && !string.IsNullOrWhiteSpace(_e.FullNameAr)
        ? _e.FullNameAr : _e.FullNameEn;

    public string Status => DaysLeft < 0
        ? (LanguageManager.Instance.IsArabic ? $"منتهٍ منذ {-DaysLeft} يوم" : $"Expired {-DaysLeft}d ago")
        : (LanguageManager.Instance.IsArabic ? $"يتبقى {DaysLeft} يوم" : $"{DaysLeft}d left");
}
