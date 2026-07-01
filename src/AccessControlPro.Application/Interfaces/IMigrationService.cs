namespace AccessControlPro.Application.Interfaces;

public class MigrationPreview
{
    public int TotalRows { get; set; }
    public int DuplicateCards { get; set; }
    public int NewPlayers { get; set; }
    public List<string> SampleNames { get; set; } = new();
}

public class MigrationResult
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
    public string ReportPath { get; set; } = "";   // saved import-report file
}

public interface IMigrationService
{
    Task<MigrationPreview> PreviewAsync(string connectionString, string tableName);
    Task<MigrationResult> ImportAsync(string connectionString, string tableName,
        IProgress<(int current, int total, string name)>? progress = null);
}
