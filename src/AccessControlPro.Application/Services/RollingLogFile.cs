using System.Globalization;
using System.IO;
using System.Text;

namespace AccessControlPro.Application.Services;

public static class RollingLogFile
{
    private const int RetentionDays = 60;
    private const string Separator = "#############################################################";
    private static readonly string[] DatedFormats = { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff" };

    private static readonly object _lock = new();
    private static readonly Dictionary<string, bool> _launchHandled = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, DateTime> _lastDate = new(StringComparer.OrdinalIgnoreCase);

    public static void Append(string path, string message)
    {
        try
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                var output = new StringBuilder();

                if (!_launchHandled.GetValueOrDefault(path))
                {
                    MigrateLegacyIfNeeded(path);
                    TrimOldEntries(path, RetentionDays);
                    output.AppendLine(Separator);
                    output.AppendLine($"# APP LAUNCH — {now:yyyy-MM-dd HH:mm:ss}");
                    output.AppendLine(Separator);
                    _launchHandled[path] = true;
                    _lastDate[path] = now.Date;
                }
                else if (now.Date != _lastDate.GetValueOrDefault(path))
                {
                    output.AppendLine(Separator);
                    output.AppendLine($"# NEW DAY — {now:yyyy-MM-dd}");
                    output.AppendLine(Separator);
                    _lastDate[path] = now.Date;
                }

                output.Append(message);
                File.AppendAllText(path, output.ToString());
            }
        }
        catch { }
    }

    private static void MigrateLegacyIfNeeded(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0) return;

            bool isNewFormat = false;
            using (var reader = new StreamReader(path))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    if (line[0] == '[')
                    {
                        var end = line.IndexOf(']');
                        if (end > 1 && DateTime.TryParseExact(
                            line.Substring(1, end - 1),
                            DatedFormats,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out _))
                        {
                            isNewFormat = true;
                        }
                    }
                    break;
                }
            }

            if (isNewFormat) return;

            File.Move(path, BuildLegacyPath(path));
        }
        catch { }
    }

    private static string BuildLegacyPath(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var stamp = DateTime.Now.ToString("yyyyMMdd");
        var candidate = Path.Combine(dir, $"{name}_legacy_{stamp}{ext}");
        int counter = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(dir, $"{name}_legacy_{stamp}_{counter}{ext}");
            counter++;
        }
        return candidate;
    }

    private static void TrimOldEntries(string path, int keepDays)
    {
        var tempPath = path + ".trim.tmp";
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0) return;

            var cutoff = DateTime.Now.AddDays(-keepDays);

            using (var reader = new StreamReader(path))
            using (var writer = new StreamWriter(tempPath, append: false))
            {
                var pendingSeparator = new List<string>();
                bool keeping = false;
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("#"))
                    {
                        pendingSeparator.Add(line);
                        continue;
                    }

                    if (line.Length > 2 && line[0] == '[')
                    {
                        var end = line.IndexOf(']');
                        if (end > 1 && DateTime.TryParseExact(
                            line.Substring(1, end - 1),
                            DatedFormats,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var ts))
                        {
                            keeping = ts >= cutoff;
                        }
                        else
                        {
                            keeping = false;
                        }
                    }

                    if (keeping)
                    {
                        foreach (var sep in pendingSeparator) writer.WriteLine(sep);
                        pendingSeparator.Clear();
                        writer.WriteLine(line);
                    }
                    else
                    {
                        pendingSeparator.Clear();
                    }
                }
            }

            File.Delete(path);
            File.Move(tempPath, path);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }
}
