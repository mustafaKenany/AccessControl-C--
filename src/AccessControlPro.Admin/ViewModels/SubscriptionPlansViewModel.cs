using System.Collections.ObjectModel;
using AccessControlPro.WPF.Helpers;
using SubscriptionPlan = AccessControlPro.Domain.Entities.SubscriptionPlan;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.SqlClient;

namespace AccessControlPro.Admin.ViewModels;

public partial class SubscriptionPlansViewModel : ObservableObject
{
    private readonly string _connectionString;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private int? _editingId;

    // Edit fields
    [ObservableProperty] private string _editNameEn = "";
    [ObservableProperty] private string _editNameAr = "";
    [ObservableProperty] private string _editDuration = "30";
    [ObservableProperty] private string _editPrice = "0";
    [ObservableProperty] private string _editMaxVisits = "0";
    [ObservableProperty] private string _editEffectiveTimes = "65535";
    [ObservableProperty] private SubscriptionPlan? _selectedPlan;

    public List<string> DurationTypes { get; } = new() { "Days", "Months", "Unlimited" };

    [ObservableProperty] private string _selectedDurationType = "Days";

    public ObservableCollection<SubscriptionPlan> Plans { get; } = new();

    public SubscriptionPlansViewModel(string connectionString)
    {
        _connectionString = connectionString;
    }

    partial void OnSelectedPlanChanged(SubscriptionPlan? value)
    {
        if (value != null)
        {
            EditNameEn = value.NameEn;
            EditNameAr = value.NameAr;
            EditDuration = value.Duration.ToString();
            SelectedDurationType = value.DurationType;
            EditPrice = value.Price.ToString("0.##");
            EditMaxVisits = value.MaxVisits.ToString();
            EditEffectiveTimes = value.EffectiveTimes.ToString();
            EditingId = value.Id;
            IsEditing = true;
        }
        else
        {
            ClearEditFields();
        }
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var plans = new List<SubscriptionPlan>();

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(
                @"SELECT Id, NameEn, NameAr, Duration, DurationType, Price, MaxVisits,
                  EffectiveTimes, IsActive, SortOrder, CreatedAt
                  FROM SubscriptionPlans ORDER BY SortOrder, Id", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                plans.Add(new SubscriptionPlan
                {
                    Id = reader.GetInt32(0),
                    NameEn = reader.GetString(1),
                    NameAr = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Duration = reader.GetInt32(3),
                    DurationType = reader.GetString(4),
                    Price = reader.GetDecimal(5),
                    MaxVisits = reader.GetInt32(6),
                    EffectiveTimes = reader.GetInt32(7),
                    IsActive = reader.GetBoolean(8),
                    SortOrder = reader.GetInt32(9),
                    CreatedAt = reader.GetDateTime(10)
                });
            }

            Plans.Clear();
            foreach (var p in plans)
                Plans.Add(p);
        }
        catch (Exception ex)
        {
            // Table may not exist yet on first run
            if (!ex.Message.Contains("Invalid object name"))
                CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditNameEn))
        {
            CustomMessageBox.Show(
                Lang.IsArabic ? "الاسم بالإنجليزية مطلوب" : "English name is required",
                Lang.ValidationTitle, MsgType.Warning);
            return;
        }

        int.TryParse(EditDuration, out int duration);
        decimal.TryParse(EditPrice, out decimal price);
        int.TryParse(EditMaxVisits, out int maxVisits);
        int.TryParse(EditEffectiveTimes, out int effectiveTimes);
        if (effectiveTimes <= 0) effectiveTimes = 65535;

        try
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            if (IsEditing && EditingId.HasValue)
            {
                using var cmd = new SqlCommand(
                    @"UPDATE SubscriptionPlans SET NameEn = @nameEn, NameAr = @nameAr,
                      Duration = @duration, DurationType = @durationType, Price = @price,
                      MaxVisits = @maxVisits, EffectiveTimes = @effectiveTimes
                      WHERE Id = @id", conn);
                cmd.Parameters.AddWithValue("@nameEn", EditNameEn.Trim());
                cmd.Parameters.AddWithValue("@nameAr", EditNameAr.Trim());
                cmd.Parameters.AddWithValue("@duration", duration);
                cmd.Parameters.AddWithValue("@durationType", SelectedDurationType);
                cmd.Parameters.AddWithValue("@price", price);
                cmd.Parameters.AddWithValue("@maxVisits", maxVisits);
                cmd.Parameters.AddWithValue("@effectiveTimes", effectiveTimes);
                cmd.Parameters.AddWithValue("@id", EditingId.Value);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                var maxOrder = Plans.Count > 0 ? Plans.Max(p => p.SortOrder) : 0;
                // Set IsActive + CreatedAt explicitly. On installs where the table was created
                // by EF (which doesn't emit SQL DEFAULT constraints for C# property initializers),
                // omitting IsActive made the INSERT fail with "Cannot insert NULL into IsActive".
                using var cmd = new SqlCommand(
                    @"INSERT INTO SubscriptionPlans (NameEn, NameAr, Duration, DurationType, Price, MaxVisits, EffectiveTimes, SortOrder, IsActive, CreatedAt)
                      VALUES (@nameEn, @nameAr, @duration, @durationType, @price, @maxVisits, @effectiveTimes, @sortOrder, 1, @createdAt)", conn);
                cmd.Parameters.AddWithValue("@nameEn", EditNameEn.Trim());
                cmd.Parameters.AddWithValue("@nameAr", EditNameAr.Trim());
                cmd.Parameters.AddWithValue("@duration", duration);
                cmd.Parameters.AddWithValue("@durationType", SelectedDurationType);
                cmd.Parameters.AddWithValue("@price", price);
                cmd.Parameters.AddWithValue("@maxVisits", maxVisits);
                cmd.Parameters.AddWithValue("@effectiveTimes", effectiveTimes);
                cmd.Parameters.AddWithValue("@sortOrder", maxOrder + 1);
                cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync();
            }

            ClearEditFields();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(SubscriptionPlan? plan)
    {
        if (plan == null) return;

        try
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(
                "UPDATE SubscriptionPlans SET IsActive = @active WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@active", !plan.IsActive);
            cmd.Parameters.AddWithValue("@id", plan.Id);
            await cmd.ExecuteNonQueryAsync();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedPlan == null) return;

        if (!CustomMessageBox.Confirm(
            Lang.IsArabic ? $"هل تريد حذف \"{SelectedPlan.NameAr}\"?" : $"Delete \"{SelectedPlan.NameEn}\"?",
            Lang.IsArabic ? "تأكيد الحذف" : "Confirm Delete"))
            return;

        try
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand("DELETE FROM SubscriptionPlans WHERE Id = @id", conn);
            cmd.Parameters.AddWithValue("@id", SelectedPlan.Id);
            await cmd.ExecuteNonQueryAsync();

            ClearEditFields();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        ClearEditFields();
    }

    private void ClearEditFields()
    {
        EditNameEn = "";
        EditNameAr = "";
        EditDuration = "30";
        SelectedDurationType = "Days";
        EditPrice = "0";
        EditMaxVisits = "0";
        EditEffectiveTimes = "65535";
        SelectedPlan = null;
        IsEditing = false;
        EditingId = null;
    }
}
