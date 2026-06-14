namespace AccessControlPro.Application.DTOs;

public class EmployeeDto
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
    public decimal RemainingBalance => SubscriptionFee - AmountPaid;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsFrozen { get; set; }
    public DateTime? FreezeStartDate { get; set; }
    public int CardCount { get; set; }
    public decimal CardBalance { get; set; }
    public decimal Debt { get; set; }   // owed from POS credit sales
    public int MaxVisits { get; set; }
    public int UsedVisits { get; set; }
    public int RemainingVisits => MaxVisits > 0 ? Math.Max(0, MaxVisits - UsedVisits) : -1; // -1 = unlimited
    public DateTime CreatedAt { get; set; }
    /// <summary>0=NoCard, 1=NotSynced, 2=PartiallySynced, 3=FullySynced</summary>
    public int SyncStatus { get; set; }
    public List<AccessCardDto> Cards { get; set; } = new();
}
