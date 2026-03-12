namespace AccessControlPro.Application.DTOs;

public class AuditLogDto
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string Details { get; set; } = string.Empty;
    public string DetailsAr { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsArabic { get; set; }
    public string DisplayDetails => IsArabic && !string.IsNullOrWhiteSpace(DetailsAr) ? DetailsAr : Details;
}
