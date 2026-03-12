namespace AccessControlPro.Domain.Entities;

public class LookupItem
{
    public int Id { get; set; }
    public string Category { get; set; } = "";     // e.g. "IncomeCategory", "ExpenseCategory", "ProductCategory", "SubscriptionPlan"
    public string Name { get; set; } = "";          // English name
    public string NameAr { get; set; } = "";        // Arabic name
    public decimal NumericValue { get; set; }        // e.g. MonthlyRate for subscription plans
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
