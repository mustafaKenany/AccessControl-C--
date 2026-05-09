using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Writes a crash log entry enriched with WPF runtime context (active window, focused control,
/// open windows, memory pressure, process uptime, thread). Defensive — every section is
/// independently try/catch'd so partial capture failure can't suppress the rest of the log.
/// </summary>
public static class CrashContextLogger
{
    public static void Write(string logPath, string source, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine();
            sb.AppendLine($"=== [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {source} ===");

            AppendException(sb, ex);
            AppendAppContext(sb);
            AppendRecentActivity(sb);
            AppendProcessInfo(sb);

            sb.AppendLine("=== end ===");
            File.AppendAllText(logPath, sb.ToString());
        }
        catch { /* Last resort — even logging itself failed */ }
    }

    private static void AppendException(StringBuilder sb, Exception? ex)
    {
        if (ex == null) { sb.AppendLine("Exception: <null>"); return; }
        sb.AppendLine($"Exception: {ex.GetType().FullName}: {ex.Message}");
        sb.AppendLine("Stack:");
        sb.AppendLine(ex.StackTrace ?? "<no stack>");

        int depth = 1;
        var inner = ex.InnerException;
        while (inner != null && depth <= 5)
        {
            sb.AppendLine($"--- Inner #{depth}: {inner.GetType().FullName}: {inner.Message}");
            sb.AppendLine(inner.StackTrace ?? "<no stack>");
            inner = inner.InnerException;
            depth++;
        }
    }

    private static void AppendAppContext(StringBuilder sb)
    {
        sb.AppendLine("--- App context ---");
        try
        {
            var app = System.Windows.Application.Current;
            if (app == null) { sb.AppendLine("Application.Current: <null>"); return; }

            // UI access requires the dispatcher thread. Cross-thread Window.Title reads can hang
            // or throw — if we're not on the UI thread, just say so and skip the visual bits.
            if (!app.Dispatcher.CheckAccess())
            {
                sb.AppendLine("(not on UI thread — window/focus details skipped)");
                return;
            }

            var main = app.MainWindow;
            sb.AppendLine($"MainWindow: {Describe(main)}");

            // Snapshot the windows collection — modifying during iteration would throw
            var windows = new List<Window>();
            foreach (Window w in app.Windows) windows.Add(w);

            sb.AppendLine($"Open windows ({windows.Count}):");
            foreach (var w in windows)
                sb.AppendLine($"  - {Describe(w)}");

            // Focused element on the active window — most useful for crashes triggered by a control
            var active = windows.Find(w => w.IsActive) ?? main;
            if (active != null)
            {
                var focused = FocusManager.GetFocusedElement(active);
                if (focused is FrameworkElement fe)
                {
                    var name = string.IsNullOrEmpty(fe.Name) ? "<unnamed>" : fe.Name;
                    sb.AppendLine($"Focused: {fe.GetType().Name} Name=\"{name}\"");

                    // Walk up the visual tree to give context (max 6 levels — keep log compact)
                    var ancestors = new List<string>();
                    DependencyObject? cur = fe;
                    int hops = 0;
                    while (cur != null && hops < 6)
                    {
                        ancestors.Add(cur.GetType().Name);
                        cur = VisualTreeHelper.GetParent(cur);
                        hops++;
                    }
                    sb.AppendLine($"Visual ancestors: {string.Join(" → ", ancestors)}");
                }
                else
                {
                    sb.AppendLine($"Focused: {focused?.GetType().Name ?? "<none>"}");
                }

                // Mouse position (helps reproduce visual-tree crashes triggered by hover)
                try
                {
                    var pt = Mouse.GetPosition(active);
                    sb.AppendLine($"Mouse position (in active window): {pt.X:F0},{pt.Y:F0}");
                }
                catch { /* not always available */ }
            }
        }
        catch (Exception ctxEx)
        {
            sb.AppendLine($"(error capturing app context: {ctxEx.GetType().Name}: {ctxEx.Message})");
        }
    }

    private static string Describe(Window? w)
    {
        if (w == null) return "<null>";
        var title = string.IsNullOrEmpty(w.Title) ? "<no title>" : w.Title;
        return $"{w.GetType().Name} \"{title}\" Visible={w.IsVisible} Active={w.IsActive}";
    }

    private static void AppendRecentActivity(StringBuilder sb)
    {
        sb.AppendLine("--- Recent user activity (oldest → newest) ---");
        try
        {
            var entries = ActivityLogger.GetRecentBreadcrumbs();
            if (entries.Count == 0)
            {
                sb.AppendLine("(no activity recorded this session)");
                return;
            }
            foreach (var entry in entries)
                sb.AppendLine($"  {entry}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"(error reading activity: {ex.GetType().Name}: {ex.Message})");
        }
    }

    private static void AppendProcessInfo(StringBuilder sb)
    {
        sb.AppendLine("--- Process info ---");
        try
        {
            var proc = Process.GetCurrentProcess();
            var uptime = DateTime.Now - proc.StartTime;
            sb.AppendLine($"PID: {proc.Id}, uptime: {uptime:dd\\.hh\\:mm\\:ss}");
            sb.AppendLine($"Working set: {proc.WorkingSet64 / (1024 * 1024)} MB, " +
                          $"private: {proc.PrivateMemorySize64 / (1024 * 1024)} MB, " +
                          $"managed heap: {GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024)} MB");
            sb.AppendLine($"GC collections (gen0/1/2): {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");

            var ver = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
            sb.AppendLine($"App version: {ver}, Windows user: {Environment.UserName}");
            sb.AppendLine($"OS: {Environment.OSVersion}, .NET: {Environment.Version}");

            var th = Thread.CurrentThread;
            var thName = string.IsNullOrEmpty(th.Name) ? "<unnamed>" : th.Name;
            sb.AppendLine($"Thread: id={th.ManagedThreadId} name=\"{thName}\" apartment={th.GetApartmentState()}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"(error capturing process info: {ex.GetType().Name}: {ex.Message})");
        }
    }
}
