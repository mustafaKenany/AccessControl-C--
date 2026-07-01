using System.ComponentModel;
using System.Windows;
using AccessControlPro.Application.Interfaces;
using AccessControlPro.WPF.Helpers;
using Microsoft.Data.SqlClient;

namespace AccessControlPro.WPF.Views;

public partial class MigrationDialog : Window, INotifyPropertyChanged
{
    private readonly IMigrationService _migrationService;
    private bool _isBusy;
    private bool _canImport;

    public LanguageManager Lang => LanguageManager.Instance;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsNotBusy));
            OnPropertyChanged(nameof(CanImport));
        }
    }

    public bool IsNotBusy => !_isBusy;

    public bool CanImport
    {
        get => _canImport && !_isBusy;
        set
        {
            _canImport = value;
            OnPropertyChanged(nameof(CanImport));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MigrationDialog(IMigrationService migrationService)
    {
        _migrationService = migrationService;
        InitializeComponent();
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        var connStr = ConnectionStringBox.Text.Trim();
        if (string.IsNullOrEmpty(connStr))
        {
            CustomMessageBox.Show(Lang.MigFillFields, Lang.ValidationTitle, MsgType.Warning, this);
            return;
        }

        IsBusy = true;
        ResultsText.Text = Lang.MigTestingConnection;

        try
        {
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            ResultsText.Text = Lang.MigConnectionSuccess;
            CustomMessageBox.Show(Lang.MigConnectionSuccess, Lang.MigTestConnection, MsgType.Success, this);
        }
        catch (Exception ex)
        {
            ResultsText.Text = $"{Lang.MigConnectionFailed}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        var connStr = ConnectionStringBox.Text.Trim();
        var tableName = TableNameBox.Text.Trim();

        if (string.IsNullOrEmpty(connStr) || string.IsNullOrEmpty(tableName))
        {
            CustomMessageBox.Show(Lang.MigFillFields, Lang.ValidationTitle, MsgType.Warning, this);
            return;
        }

        IsBusy = true;
        ResultsText.Text = Lang.MigConnecting;

        try
        {
            var preview = await _migrationService.PreviewAsync(connStr, tableName);

            var sampleList = preview.SampleNames.Count > 0
                ? "\n" + Lang.MigSampleNames + "\n  " + string.Join("\n  ", preview.SampleNames)
                : "";

            ResultsText.Text =
                $"{Lang.MigTotalRows}: {preview.TotalRows}\n" +
                $"{Lang.MigNewPlayers}: {preview.NewPlayers}\n" +
                $"{Lang.MigDuplicates}: {preview.DuplicateCards}\n" +
                sampleList;

            CanImport = preview.NewPlayers > 0;
        }
        catch (Exception ex)
        {
            ResultsText.Text = $"{Lang.MigError}: {ex.Message}";
            CanImport = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var connStr = ConnectionStringBox.Text.Trim();
        var tableName = TableNameBox.Text.Trim();

        var confirm = CustomMessageBox.Confirm(
            Lang.MigConfirmImport, Lang.MigDataMigration, MsgType.Warning, this);
        if (!confirm) return;

        IsBusy = true;
        CanImport = false;
        ProgressBar.Value = 0;

        var progress = new Progress<(int current, int total, string name)>(p =>
        {
            var pct = p.total > 0 ? (int)(p.current * 100.0 / p.total) : 0;
            ProgressBar.Value = pct;
            ProgressText.Text = $"{p.current} / {p.total} — {p.name}";
        });

        try
        {
            var result = await _migrationService.ImportAsync(connStr, tableName, progress);

            var errText = result.Errors.Count > 0
                ? "\n\n" + Lang.MigErrors + ":\n" + string.Join("\n", result.Errors.Take(10))
                : "";

            var reportLine = string.IsNullOrEmpty(result.ReportPath)
                ? ""
                : "\n\n" + (Lang.IsArabic ? "تقرير الاستيراد: " : "Import report: ") + result.ReportPath;

            ResultsText.Text =
                $"{Lang.MigImportComplete}\n\n" +
                $"{Lang.MigImported}: {result.Imported}\n" +
                $"{Lang.MigSkipped}: {result.Skipped}\n" +
                $"{Lang.MigFailedCount}: {result.Failed}" +
                errText + reportLine;

            CustomMessageBox.Show(
                $"{Lang.MigImportComplete}\n{Lang.MigImported}: {result.Imported}",
                Lang.MigDataMigration, MsgType.Info, this);
        }
        catch (Exception ex)
        {
            ResultsText.Text = $"{Lang.MigError}: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ProgressBar.Value = 0;
            ProgressText.Text = "";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
