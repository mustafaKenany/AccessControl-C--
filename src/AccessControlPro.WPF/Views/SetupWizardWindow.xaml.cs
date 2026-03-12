using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AccessControlPro.Domain.Entities;
using AccessControlPro.Infrastructure.Persistence;
using AccessControlPro.WPF.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.WPF.Views;

public partial class SetupWizardWindow : Window
{
    private int _currentStep = 1;
    private const int TotalSteps = 5;
    private bool _dbTestPassed;
    private string _connectionString = "";

    /// <summary>True if setup completed successfully.</summary>
    public bool SetupCompleted { get; private set; }

    public SetupWizardWindow()
    {
        InitializeComponent();
        UpdateStepUI();
    }

    private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    #region Navigation

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 6)
        {
            // Finish
            SetupCompleted = true;
            DialogResult = true;
            Close();
            return;
        }

        if (_currentStep == 5)
        {
            // Install
            _ = RunInstallAsync();
            return;
        }

        // Validate current step before advancing
        if (!ValidateCurrentStep()) return;

        _currentStep++;
        if (_currentStep == 5) BuildSummary();
        UpdateStepUI();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep > 1 && _currentStep <= TotalSteps)
        {
            _currentStep--;
            UpdateStepUI();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to cancel the setup?\nThe application cannot run without completing setup.",
            "Cancel Setup", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            SetupCompleted = false;
            DialogResult = false;
            Close();
        }
    }

    #endregion

    #region Step UI

    private void UpdateStepUI()
    {
        // Hide all steps
        Step1_Welcome.Visibility = Visibility.Collapsed;
        Step2_Database.Visibility = Visibility.Collapsed;
        Step3_GymSettings.Visibility = Visibility.Collapsed;
        Step4_AdminAccount.Visibility = Visibility.Collapsed;
        Step5_Summary.Visibility = Visibility.Collapsed;
        Step6_Complete.Visibility = Visibility.Collapsed;

        // Show current step
        switch (_currentStep)
        {
            case 1: Step1_Welcome.Visibility = Visibility.Visible; break;
            case 2: Step2_Database.Visibility = Visibility.Visible; break;
            case 3: Step3_GymSettings.Visibility = Visibility.Visible; break;
            case 4: Step4_AdminAccount.Visibility = Visibility.Visible; break;
            case 5: Step5_Summary.Visibility = Visibility.Visible; break;
            case 6: Step6_Complete.Visibility = Visibility.Visible; break;
        }

        // Update progress
        StepProgress.Value = _currentStep;
        StepIndicator.Text = _currentStep <= TotalSteps
            ? $"Step {_currentStep} of {TotalSteps}"
            : "Complete";

        // Back button
        BackButton.Visibility = _currentStep > 1 && _currentStep <= TotalSteps
            ? Visibility.Visible : Visibility.Collapsed;

        // Cancel button
        CancelButton.Visibility = _currentStep < 6 ? Visibility.Visible : Visibility.Collapsed;

        // Next button text
        switch (_currentStep)
        {
            case 5:
                NextButtonText.Text = "Install";
                NextButtonIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.Download;
                break;
            case 6:
                NextButtonText.Text = "Finish";
                NextButtonIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.Check;
                BackButton.Visibility = Visibility.Collapsed;
                break;
            default:
                NextButtonText.Text = "Next";
                NextButtonIcon.Icon = FontAwesome.WPF.FontAwesomeIcon.ArrowRight;
                break;
        }
    }

    #endregion

    #region Validation

    private bool ValidateCurrentStep()
    {
        switch (_currentStep)
        {
            case 1: // Welcome - developer info
                if (string.IsNullOrWhiteSpace(DevCompanyNameBox.Text))
                {
                    CustomMessageBox.Show("Please enter the developer company name.", "Validation", MsgType.Warning, this);
                    return false;
                }
                return true;

            case 2: // Database
                if (!_dbTestPassed)
                {
                    CustomMessageBox.Show("Please test the database connection before continuing.", "Validation", MsgType.Warning, this);
                    return false;
                }
                return true;

            case 3: // Gym settings
                if (string.IsNullOrWhiteSpace(GymNameEnBox.Text))
                {
                    CustomMessageBox.Show("Please enter the gym name in English.", "Validation", MsgType.Warning, this);
                    return false;
                }
                return true;

            case 4: // Admin account
                if (string.IsNullOrWhiteSpace(AdminUsernameBox.Text))
                {
                    CustomMessageBox.Show("Please enter an admin username.", "Validation", MsgType.Warning, this);
                    return false;
                }
                if (AdminPasswordBox.Password.Length < 6)
                {
                    CustomMessageBox.Show("Password must be at least 6 characters.", "Validation", MsgType.Warning, this);
                    return false;
                }
                if (AdminPasswordBox.Password != AdminConfirmPasswordBox.Password)
                {
                    CustomMessageBox.Show("Passwords do not match.", "Validation", MsgType.Warning, this);
                    return false;
                }
                return true;

            default:
                return true;
        }
    }

    #endregion

    #region Database Test

    private async void TestDbButton_Click(object sender, RoutedEventArgs e)
    {
        var server = DbServerBox.Text.Trim();
        var db = DbNameBox.Text.Trim();
        var user = DbUserBox.Text.Trim();
        var pass = DbPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(db)
            || string.IsNullOrWhiteSpace(user))
        {
            DbStatusText.Text = "Please fill in all fields.";
            DbStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
            return;
        }

        TestDbButton.IsEnabled = false;
        DbStatusText.Text = "Testing connection...";
        DbStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));

        _connectionString = DbConnectionHelper.BuildConnectionString(server, db, user, pass);

        // Test connection to the SERVER first (not the specific DB - it may not exist yet)
        var serverConnStr = DbConnectionHelper.BuildConnectionString(server, "master", user, pass);
        var error = await Task.Run(() => DbConnectionHelper.TestConnection(serverConnStr, 5));

        if (error == null)
        {
            DbStatusText.Text = "Connection successful! Database will be created if it doesn't exist.";
            DbStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0xD4, 0x7A));
            _dbTestPassed = true;
        }
        else
        {
            DbStatusText.Text = $"Failed: {error}";
            DbStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
            _dbTestPassed = false;
        }

        TestDbButton.IsEnabled = true;
    }

    #endregion

    #region Summary

    private void BuildSummary()
    {
        var lang = (LanguageCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "en";
        SummaryText.Text =
            $"Database Server:  {DbServerBox.Text.Trim()}\n" +
            $"Database Name:    {DbNameBox.Text.Trim()}\n" +
            $"DB Username:      {DbUserBox.Text.Trim()}\n" +
            $"─────────────────────────────\n" +
            $"Gym Name (EN):    {GymNameEnBox.Text.Trim()}\n" +
            $"Gym Name (AR):    {GymNameArBox.Text.Trim()}\n" +
            $"Phone:            {GymPhoneBox.Text.Trim()}\n" +
            $"Address:          {GymAddressBox.Text.Trim()}\n" +
            $"Default Language: {(lang == "ar" ? "Arabic" : "English")}\n" +
            $"─────────────────────────────\n" +
            $"Admin Username:   {AdminUsernameBox.Text.Trim()}\n" +
            $"Admin Display:    {AdminDisplayNameBox.Text.Trim()}\n" +
            $"─────────────────────────────\n" +
            $"Developer:        {DevCompanyNameBox.Text.Trim()}\n" +
            $"Support Phone:    {DevPhoneBox.Text.Trim()}";
    }

    #endregion

    #region Install

    private async Task RunInstallAsync()
    {
        NextButton.IsEnabled = false;
        BackButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        InstallStatusText.Visibility = Visibility.Visible;
        InstallProgress.Visibility = Visibility.Visible;

        // Capture all UI values on UI thread before any background work
        var adminUsername = AdminUsernameBox.Text.Trim();
        var adminPassword = AdminPasswordBox.Password;
        var adminDisplayName = AdminDisplayNameBox.Text.Trim();
        var devCompanyName = DevCompanyNameBox.Text.Trim();
        var gymNameEn = GymNameEnBox.Text.Trim();
        var gymPhone = GymPhoneBox.Text.Trim();
        var gymAddress = GymAddressBox.Text.Trim();
        var createDesktopShortcut = CreateDesktopShortcutCheck.IsChecked == true;
        var createStartMenuShortcut = CreateStartMenuCheck.IsChecked == true;

        try
        {
            // Step 0: Check for previous version
            InstallStatusText.Text = "Checking for previous installation...";
            var dbExists = await Task.Run(() => CheckDatabaseExists());
            if (dbExists)
            {
                var result = MessageBox.Show(
                    "A previous installation was detected (database already exists).\n\n" +
                    "• Click YES to drop the old database and start fresh.\n" +
                    "• Click NO to keep existing data and update settings only.\n" +
                    "• Click Cancel to abort installation.",
                    "Previous Installation Detected",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Cancel)
                {
                    InstallProgress.Visibility = Visibility.Collapsed;
                    InstallStatusText.Visibility = Visibility.Collapsed;
                    NextButton.IsEnabled = true;
                    BackButton.IsEnabled = true;
                    CancelButton.IsEnabled = true;
                    return;
                }

                if (result == MessageBoxResult.Yes)
                {
                    InstallStatusText.Text = "Removing previous database...";
                    await Task.Run(() => DropDatabase());
                }
            }

            // Step 1: Remove old shortcuts
            InstallStatusText.Text = "Cleaning up old shortcuts...";
            RemoveOldShortcuts();
            await Task.Delay(200);

            // Step 2: Save appsettings.json
            InstallStatusText.Text = "Saving configuration...";
            await Task.Delay(300);
            SaveAppSettings();

            // Step 3: Create/migrate database
            InstallStatusText.Text = "Creating database and tables...";
            await Task.Run(() => CreateDatabase());

            // Step 4: Seed admin user + gym settings
            InstallStatusText.Text = "Creating admin account and gym settings...";
            await Task.Run(() => SeedData(adminUsername, adminPassword, adminDisplayName,
                devCompanyName, gymNameEn, gymPhone, gymAddress));

            // Step 5: Copy appsettings.json to Admin & POS apps
            InstallStatusText.Text = "Configuring all applications...";
            CopyAppSettingsToSiblingApps();
            await Task.Delay(200);

            // Step 6: Create shortcuts for all 3 apps
            InstallStatusText.Text = "Creating shortcuts...";
            CreateShortcuts(createDesktopShortcut, createStartMenuShortcut);
            await Task.Delay(300);

            // Step 7: Save setup completion marker for all apps
            InstallStatusText.Text = "Finalizing setup...";
            SaveSetupMarkerForAllApps();
            await Task.Delay(300);

            // Done - move to complete step
            InstallProgress.Visibility = Visibility.Collapsed;
            InstallStatusText.Visibility = Visibility.Collapsed;
            NextButton.IsEnabled = true;
            _currentStep = 6;
            UpdateStepUI();
        }
        catch (Exception ex)
        {
            InstallProgress.Visibility = Visibility.Collapsed;
            InstallStatusText.Text = $"Installation failed: {ex.Message}";
            InstallStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));

            NextButton.IsEnabled = true;
            BackButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
    }

    private void SaveAppSettings()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        var root = new JsonObject
        {
            ["ConnectionStrings"] = new JsonObject
            {
                ["DefaultConnection"] = _connectionString
            },
            ["Developer"] = new JsonObject
            {
                ["CompanyName"] = DevCompanyNameBox.Text.Trim(),
                ["Phone"] = DevPhoneBox.Text.Trim(),
                ["Email"] = DevEmailBox.Text.Trim(),
                ["WhatsApp"] = DevWhatsAppBox.Text.Trim()
            }
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(settingsPath, root.ToJsonString(options));
    }

    private void CreateDatabase()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(_connectionString);

        using var db = new AppDbContext(optionsBuilder.Options);
        DatabaseMigrator.EnsureSchemaUpToDate(db);
    }

    private void SeedData(string adminUsername, string adminPassword, string adminDisplayName,
        string devCompanyName, string gymNameEn, string gymPhone, string gymAddress)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(_connectionString);

        using var db = new AppDbContext(optionsBuilder.Options);

        // Seed admin user (only if not exists)
        if (!db.Users.Any(u => u.Username == adminUsername))
        {
            db.Users.Add(new AppUser
            {
                Username = adminUsername,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                DisplayName = adminDisplayName,
                Role = "Admin",
                IsActive = true,
                Permissions = string.Join(",", Domain.Enums.AppPermission.All)
            });
        }

        // Seed gym settings (only if not exists)
        var existingSettings = db.Set<AppSettings>().FirstOrDefault();
        if (existingSettings == null)
        {
            db.Set<AppSettings>().Add(new AppSettings
            {
                CompanyName = devCompanyName,
                GymName = gymNameEn,
                Phone = gymPhone,
                Address = gymAddress
            });
        }
        else
        {
            existingSettings.CompanyName = devCompanyName;
            existingSettings.GymName = gymNameEn;
            existingSettings.Phone = gymPhone;
            existingSettings.Address = gymAddress;
        }

        db.SaveChanges();
    }

    private static void SaveSetupMarker()
    {
        var markerPath = Path.Combine(AppContext.BaseDirectory, ".setup_complete");
        File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
    }

    /// <summary>
    /// Saves .setup_complete marker in all app directories so none of them re-trigger the wizard.
    /// </summary>
    private static void SaveSetupMarkerForAllApps()
    {
        SaveSetupMarker(); // Current app

        var apps = FindAllAppExes();
        var timestamp = DateTime.UtcNow.ToString("O");
        foreach (var (_, exePath) in apps)
        {
            var dir = Path.GetDirectoryName(exePath);
            if (dir == null || dir == AppContext.BaseDirectory) continue;
            try
            {
                File.WriteAllText(Path.Combine(dir, ".setup_complete"), timestamp);
            }
            catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Checks if setup has been completed (marker file exists).
    /// </summary>
    public static bool IsSetupComplete()
    {
        var markerPath = Path.Combine(AppContext.BaseDirectory, ".setup_complete");
        return File.Exists(markerPath);
    }

    #endregion

    #region Previous Version Detection

    private bool CheckDatabaseExists()
    {
        try
        {
            // Parse the target DB name from connection string
            var builder = new SqlConnectionStringBuilder(_connectionString);
            var dbName = builder.InitialCatalog;
            builder.InitialCatalog = "master";

            using var conn = new SqlConnection(builder.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT DB_ID(@dbName)";
            cmd.Parameters.AddWithValue("@dbName", dbName);
            var result = cmd.ExecuteScalar();
            return result != null && result != DBNull.Value;
        }
        catch
        {
            return false;
        }
    }

    private void DropDatabase()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        var dbName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        using var conn = new SqlConnection(builder.ConnectionString);
        conn.Open();

        // Kill active connections first
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $@"
                IF DB_ID(@dbName) IS NOT NULL
                BEGIN
                    ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{dbName}];
                END";
            cmd.Parameters.AddWithValue("@dbName", dbName);
            cmd.ExecuteNonQuery();
        }
    }

    #endregion

    #region Shortcuts

    private const string BrandName = "HM-GymManagement";

    /// <summary>
    /// All 3 app definitions: display name, exe file name.
    /// </summary>
    private static readonly (string DisplayName, string ExeFileName)[] AllApps =
    [
        ($"{BrandName}", "AccessControlPro.WPF.exe"),
        ($"{BrandName} Admin", "AccessControlPro.Admin.exe"),
        ($"{BrandName} POS", "AccessControlPro.POS.exe"),
    ];

    /// <summary>
    /// Finds all app executables by searching the current directory and sibling project directories.
    /// </summary>
    private static List<(string DisplayName, string ExePath)> FindAllAppExes()
    {
        var found = new List<(string DisplayName, string ExePath)>();
        var baseDir = AppContext.BaseDirectory;

        foreach (var (displayName, exeFileName) in AllApps)
        {
            // 1. Check same directory (publish/install scenario - all in one folder)
            var sameDirPath = Path.Combine(baseDir, exeFileName);
            if (File.Exists(sameDirPath))
            {
                found.Add((displayName, sameDirPath));
                continue;
            }

            // 2. Check sibling project directories (development scenario)
            // e.g. from AccessControlPro.WPF/bin/Debug/net8.0-windows/ look for AccessControlPro.Admin/bin/Debug/net8.0-windows/
            var projectName = Path.GetFileNameWithoutExtension(exeFileName);
            var srcDir = baseDir;

            // Walk up to find the project root (src folder)
            for (int i = 0; i < 6; i++)
            {
                srcDir = Path.GetDirectoryName(srcDir);
                if (srcDir == null) break;

                // Check if sibling project exists at same build path depth
                var siblingExe = Path.Combine(srcDir, projectName,
                    baseDir.Substring(baseDir.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) >= 0
                        ? baseDir.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) + 1
                        : 0),
                    exeFileName);

                // Simpler approach: look in parallel bin folders
                if (srcDir != null && Directory.Exists(Path.Combine(srcDir, projectName)))
                {
                    // Reconstruct relative bin path
                    var currentProjectDir = baseDir;
                    var binIndex = currentProjectDir.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar);
                    if (binIndex >= 0)
                    {
                        var relativeBinPath = currentProjectDir.Substring(binIndex + 1); // "bin\Debug\net8.0-windows\"
                        var siblingPath = Path.Combine(srcDir, projectName, relativeBinPath, exeFileName);
                        if (File.Exists(siblingPath))
                        {
                            found.Add((displayName, siblingPath));
                            break;
                        }
                    }
                }
            }
        }

        return found;
    }

    private static void RemoveOldShortcuts()
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        // Remove all possible desktop shortcuts (old and new names)
        foreach (var name in new[] { BrandName, $"{BrandName} Admin", $"{BrandName} POS",
            "AccessControlPro", "AccessControlPro Admin", "AccessControlPro POS" })
        {
            var lnk = Path.Combine(desktopPath, $"{name}.lnk");
            if (File.Exists(lnk)) File.Delete(lnk);
        }

        // Remove start menu folders (old and new names)
        var startMenuBase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
        foreach (var folder in new[] { BrandName, "AccessControlPro" })
        {
            var dir = Path.Combine(startMenuBase, folder);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    private static void CreateShortcuts(bool desktop, bool startMenu)
    {
        var apps = FindAllAppExes();
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var startMenuDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs", BrandName);

        if (startMenu)
            Directory.CreateDirectory(startMenuDir);

        foreach (var (displayName, exePath) in apps)
        {
            var workDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

            if (desktop)
            {
                var lnkPath = Path.Combine(desktopPath, $"{displayName}.lnk");
                CreateShortcutFile(lnkPath, exePath, workDir, displayName);
            }

            if (startMenu)
            {
                var lnkPath = Path.Combine(startMenuDir, $"{displayName}.lnk");
                CreateShortcutFile(lnkPath, exePath, workDir, displayName);
            }
        }
    }

    /// <summary>
    /// Copies appsettings.json to all sibling app directories so they share the same DB connection.
    /// </summary>
    private static void CopyAppSettingsToSiblingApps()
    {
        var sourceSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(sourceSettings)) return;

        var apps = FindAllAppExes();
        foreach (var (_, exePath) in apps)
        {
            var targetDir = Path.GetDirectoryName(exePath);
            if (targetDir == null || targetDir == AppContext.BaseDirectory) continue;

            var targetSettings = Path.Combine(targetDir, "appsettings.json");
            try
            {
                File.Copy(sourceSettings, targetSettings, overwrite: true);
            }
            catch { /* ignore - may be same file or locked */ }
        }
    }

    /// <summary>
    /// Creates a Windows .lnk shortcut file using Windows Script Host COM.
    /// </summary>
    private static void CreateShortcutFile(string lnkPath, string targetExe, string workingDir, string description)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return;

        dynamic? shell = null;
        dynamic? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell == null) return;

            shortcut = shell.CreateShortcut(lnkPath);
            shortcut.TargetPath = targetExe;
            shortcut.WorkingDirectory = workingDir;
            shortcut.Description = description;
            shortcut.IconLocation = targetExe + ",0";
            shortcut.Save();
        }
        finally
        {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }

    #endregion
}
