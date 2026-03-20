using System.IO;
using System.Text.Json;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Saves/loads pending SDK operations that require app restart to complete.
/// When addUnSortCard returns -1 after all retries, the app saves the pending
/// operation, restarts, and auto-executes it on fresh SDK initialization.
/// </summary>
public static class PendingOperationHelper
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, ".pending_operation");

    public class PendingOperation
    {
        public string Type { get; set; } = ""; // "Renew", "AssignCard", "Freeze", "Unfreeze"
        public string Username { get; set; } = "";
        public int PlayerId { get; set; }
        public string? CardNumber { get; set; }

        // Renew-specific
        public string? SubscriptionType { get; set; }
        public int Months { get; set; }
        public int CustomDays { get; set; }
        public decimal Fee { get; set; }
        public decimal AmountPaid { get; set; }
        public string? DoorPermissions { get; set; }
        public int EffectiveTimes { get; set; }
        public List<int>? DeviceIds { get; set; }
    }

    public static void Save(PendingOperation op)
    {
        try
        {
            var json = JsonSerializer.Serialize(op, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }

    public static PendingOperation? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<PendingOperation>(json);
        }
        catch { return null; }
    }

    public static void Clear()
    {
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
    }

    public static bool HasPending() => File.Exists(FilePath);
}
