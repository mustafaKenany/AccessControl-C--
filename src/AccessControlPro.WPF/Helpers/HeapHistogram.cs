using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Diagnostics.Runtime;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Captures a lightweight histogram of the app's OWN managed heap — the top object types by total
/// size and count — by taking a copy-on-write snapshot of the current process and reading it with
/// ClrMD. The output is only a few KB of text, so it drops straight into the diagnostics bundle and
/// lets a memory leak be diagnosed REMOTELY without shipping a multi-GB memory dump.
///
/// Managed heap only: if this histogram stays flat while the process working set still climbs to the
/// 32-bit ceiling, that itself proves the leak is in NATIVE (SDK) memory, not managed — which tells
/// us exactly where to look next.
/// </summary>
public static class HeapHistogram
{
    /// <summary>Snapshots the current process and returns a top-<paramref name="topN"/> managed-type
    /// histogram as text. Never throws — on failure it returns a one-line reason (also useful signal).</summary>
    public static string CaptureTopTypes(int topN = 25)
    {
        var sb = new StringBuilder();
        var started = DateTime.Now;
        try
        {
            var pid = Process.GetCurrentProcess().Id;
            using var dt = DataTarget.CreateSnapshotAndAttach(pid);
            var clrInfo = dt.ClrVersions.FirstOrDefault();
            if (clrInfo == null)
            {
                sb.AppendLine($"=== HEAP HISTOGRAM {started:yyyy-MM-dd HH:mm:ss} — no CLR runtime found ===");
                return sb.ToString();
            }

            using var runtime = clrInfo.CreateRuntime();
            var byType = new Dictionary<string, (long count, long bytes)>(StringComparer.Ordinal);
            long totalObjs = 0, totalBytes = 0;

            foreach (var obj in runtime.Heap.EnumerateObjects())
            {
                var type = obj.Type;
                if (type == null) continue;
                var name = type.Name ?? "(unknown)";
                var size = (long)obj.Size;
                byType.TryGetValue(name, out var cur);
                byType[name] = (cur.count + 1, cur.bytes + size);
                totalObjs++;
                totalBytes += size;
            }

            var ws = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
            var took = (DateTime.Now - started).TotalSeconds;
            sb.AppendLine($"=== HEAP HISTOGRAM {started:yyyy-MM-dd HH:mm:ss}  ws={ws}MB  managedObjs={totalObjs:N0}  managedHeap={totalBytes / (1024 * 1024)}MB  (capture {took:F1}s) ===");
            sb.AppendLine($"{"BYTES(MB)",10}  {"COUNT",13}  TYPE");
            foreach (var kv in byType.OrderByDescending(x => x.Value.bytes).Take(topN))
                sb.AppendLine($"{kv.Value.bytes / (1024.0 * 1024.0),10:F1}  {kv.Value.count,13:N0}  {kv.Key}");
            sb.AppendLine();
        }
        catch (Exception ex)
        {
            // Self-snapshot can fail on some configs (permissions, DAC not found). We LOSE nothing —
            // the reason line is itself signal, and the app keeps running normally.
            sb.AppendLine($"=== HEAP HISTOGRAM {started:yyyy-MM-dd HH:mm:ss} — capture FAILED: {ex.GetType().Name}: {ex.Message} ===");
        }
        return sb.ToString();
    }
}
