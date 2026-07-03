using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using AccessControlPro.Application.Services;
using AccessControlPro.Domain.Entities;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;

namespace AccessControlPro.Admin.ViewModels;

public partial class QrPoolViewModel : ObservableObject
{
    private readonly IQrPoolService _qrPoolService;
    private readonly string _connectionString;

    public LanguageManager Lang => LanguageManager.Instance;

    // Stats
    [ObservableProperty] private int _totalCodes;
    [ObservableProperty] private int _availableCodes;
    [ObservableProperty] private int _assignedCodes;
    [ObservableProperty] private int _usedCodes;
    [ObservableProperty] private int _expiredCodes;
    [ObservableProperty] private bool _needsRegeneration;
    [ObservableProperty] private bool _isLoading;

    // Assign form
    [ObservableProperty] private string _guestName = "";
    [ObservableProperty] private string _guestPhone = "";
    [ObservableProperty] private string _guestReason = "";
    [ObservableProperty] private bool _showAssignForm;

    public ObservableCollection<QrPoolEntry> AssignedList { get; } = new();

    /// <summary>Filtered view over AssignedList — search by code / guest name / phone.</summary>
    public ICollectionView AssignedView { get; }

    [ObservableProperty] private string _searchText = "";
    partial void OnSearchTextChanged(string value) => AssignedView.Refresh();

    public QrPoolViewModel(IQrPoolService qrPoolService, string connectionString)
    {
        _qrPoolService = qrPoolService;
        _connectionString = connectionString;
        AssignedView = CollectionViewSource.GetDefaultView(AssignedList);
        AssignedView.Filter = FilterAssigned;
    }

    private bool FilterAssigned(object obj)
    {
        var s = SearchText?.Trim();
        if (string.IsNullOrEmpty(s)) return true;
        if (obj is not QrPoolEntry e) return false;
        return (e.Code?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
            || (e.GuestName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
            || (e.GuestPhone?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            // Load stats via raw SQL for counts by status
            await LoadStatsAsync();

            // Load assigned codes
            var assigned = await _qrPoolService.GetAssignedAsync();
            AssignedList.Clear();
            foreach (var entry in assigned)
                AssignedList.Add(entry);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadStatsAsync()
    {
        try
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(
                @"SELECT
                    COUNT(*) AS Total,
                    SUM(CASE WHEN Status = 0 THEN 1 ELSE 0 END) AS Available,
                    SUM(CASE WHEN Status = 1 THEN 1 ELSE 0 END) AS Assigned,
                    SUM(CASE WHEN Status = 2 THEN 1 ELSE 0 END) AS Used,
                    SUM(CASE WHEN Status = 3 THEN 1 ELSE 0 END) AS Expired
                  FROM QrPool", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                TotalCodes = reader.GetInt32(0);
                AvailableCodes = reader.GetInt32(1);
                AssignedCodes = reader.GetInt32(2);
                UsedCodes = reader.GetInt32(3);
                ExpiredCodes = reader.GetInt32(4);
            }

            NeedsRegeneration = AvailableCodes < 500;
        }
        catch
        {
            // Stats unavailable — table may not exist yet
            TotalCodes = 0;
            AvailableCodes = 0;
            AssignedCodes = 0;
            UsedCodes = 0;
            ExpiredCodes = 0;
            NeedsRegeneration = false;
        }
    }

    [RelayCommand]
    private void ToggleAssignForm()
    {
        ShowAssignForm = !ShowAssignForm;
        if (!ShowAssignForm)
        {
            GuestName = "";
            GuestPhone = "";
            GuestReason = "";
        }
    }

    [RelayCommand]
    private async Task AssignAsync()
    {
        if (string.IsNullOrWhiteSpace(GuestName))
        {
            CustomMessageBox.Show(
                Lang.IsArabic ? "اسم الضيف مطلوب" : "Guest name is required",
                Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        try
        {
            var result = await _qrPoolService.AssignCodeAsync(GuestName.Trim(), GuestPhone.Trim(), GuestReason.Trim());
            if (result == null)
            {
                CustomMessageBox.Show(
                    Lang.IsArabic ? "لا توجد رموز QR متاحة. يرجى إعادة توليد المجموعة." : "No QR codes available. Please regenerate the pool.",
                    Lang.ValidationTitle, MsgType.Warning);
                return;
            }

            CustomMessageBox.Show(
                Lang.IsArabic ? $"تم تعيين الرمز: {result.Code}" : $"Assigned code: {result.Code}",
                Lang.IsArabic ? "تم التعيين" : "Assigned", MsgType.Success);

            GuestName = "";
            GuestPhone = "";
            GuestReason = "";
            ShowAssignForm = false;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync(QrPoolEntry? entry)
    {
        if (entry == null) return;

        if (!CustomMessageBox.Confirm(
            Lang.IsArabic ? $"هل تريد إلغاء تفعيل الرمز {entry.Code}?" : $"Deactivate code {entry.Code}?",
            Lang.IsArabic ? "تأكيد" : "Confirm"))
            return;

        try
        {
            await _qrPoolService.DeactivateAsync(entry.Code);
            CustomMessageBox.Show(
                Lang.IsArabic ? "تم إلغاء التفعيل" : "Code deactivated",
                Lang.IsArabic ? "تم" : "Done", MsgType.Success);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CleanupExpiredAsync()
    {
        try
        {
            var count = await _qrPoolService.CleanupExpiredAsync();
            CustomMessageBox.Show(
                Lang.IsArabic ? $"تم تنظيف {count} رمز منتهي الصلاحية" : $"Cleaned up {count} expired codes",
                Lang.IsArabic ? "تنظيف" : "Cleanup", MsgType.Success);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }
}
