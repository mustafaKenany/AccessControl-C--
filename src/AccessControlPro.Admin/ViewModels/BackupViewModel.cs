using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using AccessControlPro.Infrastructure.Persistence;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Admin.ViewModels;

public partial class BackupViewModel : ObservableObject
{
    private readonly AppDbContext _dbContext;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _lastBackupInfo = "";
    [ObservableProperty] private string _statusMessage = "";

    public BackupViewModel(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private static string SanitizeDbName(string name)
    {
        // Only allow alphanumeric, underscore, hyphen — strip everything else including brackets
        return Regex.Replace(name, @"[^\w\-]", "");
    }

    private static void ValidateBackupPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var ext = Path.GetExtension(fullPath);
        if (!string.Equals(ext, ".bak", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only .bak files are allowed.");
        // Block path traversal sequences
        if (path.Contains(".."))
            throw new InvalidOperationException("Invalid path.");
    }

    [RelayCommand]
    private async Task BackupAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Lang.IsArabic ? "حفظ نسخة احتياطية" : "Save Backup",
            Filter = "SQL Backup (*.bak)|*.bak",
            FileName = $"AccessControlPro_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak"
        };

        if (dialog.ShowDialog() != true) return;

        IsLoading = true;
        StatusMessage = Lang.IsArabic ? "جاري إنشاء النسخة الاحتياطية..." : "Creating backup...";
        try
        {
            ValidateBackupPath(dialog.FileName);
            var dbName = SanitizeDbName(_dbContext.Database.GetDbConnection().Database ?? "AccessControlPro");
            var backupPath = Path.GetFullPath(dialog.FileName);

            // Use parameterized query via sp_executesql to prevent SQL injection
            await _dbContext.Database.ExecuteSqlRawAsync(
                "BACKUP DATABASE [" + dbName + "] TO DISK = {0} WITH FORMAT, INIT",
                backupPath);

            var fileInfo = new FileInfo(dialog.FileName);
            var sizeMb = fileInfo.Length / 1024.0 / 1024.0;
            LastBackupInfo = $"{dialog.FileName} ({sizeMb:F1} MB)";
            StatusMessage = "";

            CustomMessageBox.Show(
                Lang.IsArabic ? "تم إنشاء النسخة الاحتياطية بنجاح" : "Backup created successfully",
                Lang.IsArabic ? "نجاح" : "Success", MsgType.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = "";
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Lang.IsArabic ? "اختر ملف النسخة الاحتياطية" : "Select Backup File",
            Filter = "SQL Backup (*.bak)|*.bak"
        };

        if (dialog.ShowDialog() != true) return;

        var result = MessageBox.Show(
            Lang.IsArabic
                ? "تحذير: سيتم استبدال قاعدة البيانات الحالية بالكامل. هل أنت متأكد؟"
                : "Warning: This will replace the entire current database. Are you sure?",
            Lang.IsArabic ? "تأكيد الاستعادة" : "Confirm Restore",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;
        StatusMessage = Lang.IsArabic ? "جاري استعادة قاعدة البيانات..." : "Restoring database...";
        try
        {
            ValidateBackupPath(dialog.FileName);
            var dbName = SanitizeDbName(_dbContext.Database.GetDbConnection().Database ?? "AccessControlPro");
            var backupPath = Path.GetFullPath(dialog.FileName);

            if (!File.Exists(backupPath))
                throw new FileNotFoundException("Backup file not found.");

            await _dbContext.Database.ExecuteSqlRawAsync(
                "ALTER DATABASE [" + dbName + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
            await _dbContext.Database.ExecuteSqlRawAsync(
                "RESTORE DATABASE [" + dbName + "] FROM DISK = {0} WITH REPLACE",
                backupPath);
            await _dbContext.Database.ExecuteSqlRawAsync(
                "ALTER DATABASE [" + dbName + "] SET MULTI_USER");

            StatusMessage = "";
            CustomMessageBox.Show(
                Lang.IsArabic
                    ? "تم استعادة قاعدة البيانات بنجاح. يرجى إعادة تشغيل التطبيق."
                    : "Database restored successfully. Please restart the application.",
                Lang.IsArabic ? "نجاح" : "Success", MsgType.Success);
        }
        catch (Exception ex)
        {
            StatusMessage = "";
            try
            {
                var dbName = SanitizeDbName(_dbContext.Database.GetDbConnection().Database ?? "AccessControlPro");
                await _dbContext.Database.ExecuteSqlRawAsync("ALTER DATABASE [" + dbName + "] SET MULTI_USER");
            }
            catch { }
            CustomMessageBox.Show(ex.Message, Lang.ValidationTitle, MsgType.Error);
        }
        finally { IsLoading = false; }
    }
}
