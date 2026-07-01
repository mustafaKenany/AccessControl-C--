using AccessControlPro.Application.Interfaces;

namespace AccessControlPro.Application.Services;

/// <summary>
/// Writes human-readable, per-operation report files for the two migration stages so support can see
/// exactly what happened after the fact (they also ride the diagnostics bundle to the cloud):
///   • import   — how many members came across from the old system, and every failure with its reason
///   • device sync — how many of those cards actually reached the gate, and how many failed
/// These are SUMMARIES for operators/support — distinct from the raw per-call <c>sdk_log.txt</c> trace,
/// which each device-sync report points to for low-level detail. One timestamped file per run, kept
/// under Logs/imports (last 100 pruned).
/// </summary>
public static class ImportReportLog
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "Logs", "imports");

    private static string Write(string prefix, string content)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var path = Path.Combine(Dir, $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(path, content);
            Prune();
            return path;
        }
        catch { return ""; } // reporting must never break the operation it documents
    }

    /// <summary>Keep only the newest 100 report files.</summary>
    private static void Prune()
    {
        try
        {
            var files = new DirectoryInfo(Dir).GetFiles("*.txt");
            foreach (var f in files.OrderByDescending(f => f.LastWriteTime).Skip(100))
                try { f.Delete(); } catch { }
        }
        catch { }
    }

    /// <summary>Write the import-stage report. Returns the file path (empty on failure).</summary>
    public static string WriteImportReport(string sourceTable, MigrationResult result)
    {
        int total = result.Imported + result.Skipped + result.Failed;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Data Import Report / تقرير استيراد البيانات ===");
        sb.AppendLine($"Date:         {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Source table: {sourceTable}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Processed:  {total}");
        sb.AppendLine($"Imported:   {result.Imported}");
        sb.AppendLine($"Skipped:    {result.Skipped}   (duplicate card / already exists)");
        sb.AppendLine($"Failed:     {result.Failed}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("Access cards were created but are NOT yet on the gate.");
        sb.AppendLine("Run \"Sync all players to device\", then see the matching device-sync report.");
        if (result.Errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"--- Failures ({result.Errors.Count}) ---");
            foreach (var e in result.Errors) sb.AppendLine(e);
        }
        return Write("import", sb.ToString());
    }

    /// <summary>Write the device-sync-stage report. Returns the file path (empty on failure).</summary>
    public static string WriteDeviceSyncReport(string deviceName, string deviceIp,
        int membersSynced, int membersFailed, int poolPushed, int poolFailed)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Device Sync Report / تقرير مزامنة الجهاز ===");
        sb.AppendLine($"Date:   {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Device: {deviceName} ({deviceIp})");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Members pushed OK: {membersSynced}");
        sb.AppendLine($"Members failed:    {membersFailed}");
        sb.AppendLine($"QR passes pushed:  {poolPushed}");
        sb.AppendLine($"QR passes failed:  {poolFailed}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine(membersFailed + poolFailed == 0
            ? "All records reached the gate successfully."
            : "Some records did not reach the gate. For per-card detail see sdk_log.txt (install folder).");
        return Write("device-sync", sb.ToString());
    }
}
