using AccessControlPro.Domain.Enums;

namespace AccessControlPro.Application.DTOs;

public class TransactionDto
{
    public int Id { get; set; }
    public TransactionType Type { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? RelatedEmployeeId { get; set; }
    public string? RelatedEmployeeName { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Display helpers — avoid XAML DataTrigger crashes
    public string TypeLabel => Type == TransactionType.Income ? "Income" : "Expense";
    public string TypeColor => Type == TransactionType.Income ? "#2ED47A" : "#F7685B";
    public string PaymentLabel => PaymentMethod == PaymentMethod.Cash ? "Cash" : "Card";
}
