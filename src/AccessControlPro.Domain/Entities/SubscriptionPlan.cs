namespace AccessControlPro.Domain.Entities;

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string NameEn { get; set; } = "";
    public string NameAr { get; set; } = "";
    public int Duration { get; set; } = 30;
    public string DurationType { get; set; } = "Days"; // Days, Months, Unlimited
    public decimal Price { get; set; }
    public int MaxVisits { get; set; } // 0 = unlimited
    public int EffectiveTimes { get; set; } = 65535;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
