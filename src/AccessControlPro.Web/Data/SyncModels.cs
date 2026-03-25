using System.Text.Json.Serialization;

namespace AccessControlPro.Web.Data;

public class SyncPayload
{
    [JsonPropertyName("players")]
    public List<Dictionary<string, object?>>? Players { get; set; }

    [JsonPropertyName("accessEvents")]
    public List<Dictionary<string, object?>>? AccessEvents { get; set; }

    [JsonPropertyName("devices")]
    public List<Dictionary<string, object?>>? Devices { get; set; }

    [JsonPropertyName("doors")]
    public List<Dictionary<string, object?>>? Doors { get; set; }

    [JsonPropertyName("transactions")]
    public List<Dictionary<string, object?>>? Transactions { get; set; }

    [JsonPropertyName("users")]
    public List<Dictionary<string, object?>>? Users { get; set; }

    [JsonPropertyName("auditLogs")]
    public List<Dictionary<string, object?>>? AuditLogs { get; set; }

    [JsonPropertyName("deletedEmployees")]
    public List<Dictionary<string, object?>>? DeletedEmployees { get; set; }

    [JsonPropertyName("appSettings")]
    public List<Dictionary<string, object?>>? AppSettings { get; set; }

    [JsonPropertyName("accessCards")]
    public List<Dictionary<string, object?>>? AccessCards { get; set; }

    [JsonPropertyName("qrPool")]
    public List<Dictionary<string, object?>>? QrPool { get; set; }
}
