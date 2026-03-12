namespace AccessControlPro.Application.DTOs;

public class PlayerProfileDto
{
    public EmployeeDto Player { get; set; } = null!;
    public List<FreezeHistoryDto> FreezeHistory { get; set; } = new();
    public List<TransactionDto> Transactions { get; set; } = new();
    public List<AuditLogDto> AuditLogs { get; set; } = new();
}

public class FreezeHistoryDto
{
    public DateTime FreezeStart { get; set; }
    public DateTime? FreezeEnd { get; set; }
    public int FreezeDays { get; set; }
    public string Reason { get; set; } = string.Empty;
}
