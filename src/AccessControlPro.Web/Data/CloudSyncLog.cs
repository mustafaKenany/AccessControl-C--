namespace AccessControlPro.Web.Data;

public class CloudSyncLog
{
    public int Id { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public string TableName { get; set; } = "";
    public int RecordCount { get; set; }
    public string Status { get; set; } = ""; // Success, Failed
    public string? Error { get; set; }
}
