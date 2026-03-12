namespace AccessControlPro.Domain.Entities;

public class Employee
{
    public int Id { get; set; }
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
    public bool IsFrozen { get; set; }
    public DateTime? FreezeStartDate { get; set; }
    public decimal CardBalance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optimistic concurrency token — prevents lost updates when multiple users
    /// edit the same player simultaneously (e.g., POS balance deduction + admin edit).
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public ICollection<AccessCard> AccessCards { get; set; } = new List<AccessCard>();
    public ICollection<FreezeHistory> FreezeHistories { get; set; } = new List<FreezeHistory>();
}
