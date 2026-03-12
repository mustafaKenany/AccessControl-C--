namespace AccessControlPro.Domain.Entities;

public class FreezeHistory
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime FreezeStart { get; set; }
    public DateTime? FreezeEnd { get; set; }
    public int FreezeDays { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Employee Employee { get; set; } = null!;
}
