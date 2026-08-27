using System;
using System.IO;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Remembers whether the first-run guided tour has been shown, via a tiny marker file next to the
/// app. The tour shows itself once on the first launch; after that it only runs when the operator
/// taps the "?" help button. Failures are non-fatal (a missing/locked file just means "show it").
/// </summary>
public static class TourState
{
    private static string MarkerPath => Path.Combine(AppContext.BaseDirectory, ".tour_seen");

    public static bool HasSeen()
    {
        try { return File.Exists(MarkerPath); }
        catch { return true; } // on any error, don't nag
    }

    public static void MarkSeen()
    {
        try { File.WriteAllText(MarkerPath, DateTime.Now.ToString("o")); }
        catch { /* best-effort */ }
    }
}
