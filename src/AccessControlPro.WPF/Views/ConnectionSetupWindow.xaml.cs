using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Views;

public partial class ConnectionSetupWindow : Window
{
    private readonly string _settingsPath;

    public bool IsSaved { get; private set; }
    public string ConnectionString { get; private set; } = "";

    public ConnectionSetupWindow(string? currentServer = null, string? currentDb = null,
        string? currentUser = null, string? errorMessage = null)
    {
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        InitializeComponent();

        // Pre-fill from current settings if available
        if (!string.IsNullOrWhiteSpace(currentServer))
            ServerBox.Text = currentServer;
        if (!string.IsNullOrWhiteSpace(currentDb))
            DatabaseBox.Text = currentDb;
        if (!string.IsNullOrWhiteSpace(currentUser))
            UserBox.Text = currentUser;

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            SubtitleText.Text = errorMessage;
            SubtitleText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B));
        }

        // Invalidate save when fields change
        ServerBox.TextChanged += (_, _) => OnFieldChanged();
        DatabaseBox.TextChanged += (_, _) => OnFieldChanged();
        UserBox.TextChanged += (_, _) => OnFieldChanged();
        PasswordBox.PasswordChanged += (_, _) => OnFieldChanged();
    }

    private void OnFieldChanged()
    {
        SaveButton.IsEnabled = false;
        StatusText.Visibility = Visibility.Collapsed;
    }

    private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var server = ServerBox.Text.Trim();
        var db = DatabaseBox.Text.Trim();
        var user = UserBox.Text.Trim();
        var pass = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(db)
            || string.IsNullOrWhiteSpace(user))
        {
            ShowStatus("Please fill in all fields.", isError: true);
            return;
        }

        TestButton.IsEnabled = false;
        ShowStatus("Testing connection...", isError: false);

        var connStr = DbConnectionHelper.BuildConnectionString(server, db, user, pass);

        // Run test on background thread to keep UI responsive
        var error = await Task.Run(() => DbConnectionHelper.TestConnection(connStr, 5));

        if (error == null)
        {
            ShowStatus("Connection successful!", isError: false, isSuccess: true);
            ConnectionString = connStr;
            SaveButton.IsEnabled = true;
        }
        else
        {
            ShowStatus($"Connection failed: {error}", isError: true);
            SaveButton.IsEnabled = false;
        }

        TestButton.IsEnabled = true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveConnectionString(ConnectionString);
            IsSaved = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowStatus($"Failed to save settings: {ex.Message}", isError: true);
        }
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        IsSaved = false;
        DialogResult = false;
        Close();
    }

    private void SaveConnectionString(string connectionString)
    {
        JsonNode root;

        if (File.Exists(_settingsPath))
        {
            var json = File.ReadAllText(_settingsPath);
            root = JsonNode.Parse(json) ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        var cs = root["ConnectionStrings"]?.AsObject();
        if (cs == null)
        {
            cs = new JsonObject();
            root["ConnectionStrings"] = cs;
        }

        cs["DefaultConnection"] = connectionString;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_settingsPath, root.ToJsonString(options));
    }

    private void ShowStatus(string message, bool isError, bool isSuccess = false)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;

        if (isSuccess)
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50));
        else if (isError)
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xFF, 0x6B, 0x6B));
        else
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x4F, 0xC3, 0xF7));
    }
}
