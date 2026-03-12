using AccessControlPro.Application.Interfaces;
using AccessControlPro.Domain.Entities;
using AccessControlPro.WPF.Helpers;
using AccessControlPro.WPF.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AccessControlPro.Admin.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsService _settingsService;

    public LanguageManager Lang => LanguageManager.Instance;

    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string _gymName = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _logoPath = string.Empty;
    [ObservableProperty] private bool _isLoading;

    public SettingsViewModel(IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task LoadSettingsAsync()
    {
        IsLoading = true;
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            CompanyName = settings.CompanyName;
            GymName = settings.GymName;
            Phone = settings.Phone;
            Address = settings.Address;
            LogoPath = settings.LogoPath;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void BrowseLogo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            Title = "Select Logo"
        };

        if (dialog.ShowDialog() == true)
        {
            var ext = System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant();
            var allowedExts = new[] { ".png", ".jpg", ".jpeg", ".bmp" };
            if (!allowedExts.Contains(ext))
            {
                CustomMessageBox.Show("Invalid image format.", Lang.ValidationTitle, MsgType.Warning);
                return;
            }

            // Validate file size (max 5 MB)
            var fi = new System.IO.FileInfo(dialog.FileName);
            if (fi.Length > 5 * 1024 * 1024)
            {
                CustomMessageBox.Show("Image file must be under 5 MB.", Lang.ValidationTitle, MsgType.Warning);
                return;
            }

            var destDir = System.IO.Path.Combine(AppContext.BaseDirectory, "AppData");
            System.IO.Directory.CreateDirectory(destDir);
            var destFile = System.IO.Path.Combine(destDir, "logo" + ext);
            System.IO.File.Copy(dialog.FileName, destFile, true);
            LogoPath = destFile;
        }
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                CompanyName = CompanyName,
                GymName = GymName,
                Phone = Phone,
                Address = Address,
                LogoPath = LogoPath
            };
            await _settingsService.SaveSettingsAsync(settings);
            CustomMessageBox.Show(
                Lang.SetSaved,
                Lang.NavSettings,
                MsgType.Success);
        }
        catch (Exception ex)
        {
            CustomMessageBox.Show(ex.Message, "Error", MsgType.Error);
        }
    }
}
