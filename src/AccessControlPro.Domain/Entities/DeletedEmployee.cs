namespace AccessControlPro.Domain.Entities;

public class DeletedEmployee
{
    public int Id { get; set; }
    public int OriginalId { get; set; }
    public string FullNameEn { get; set; } = string.Empty;
    public string FullNameAr { get; set; } = string.Empty;
    public string CardNo { get; set; } = string.Empty;
    public string SubscriptionType { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public byte[]? PhotoData { get; set; }
    public double Height { get; set; }
    public double Weight { get; set; }
    public decimal SubscriptionFee { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string DeleteReason { get; set; } = string.Empty;
    public string DeletedBy { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
    public DateTime OriginalCreatedAt { get; set; }
}
