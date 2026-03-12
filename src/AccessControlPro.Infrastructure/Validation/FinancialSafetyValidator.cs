namespace AccessControlPro.Infrastructure.Validation;

/// <summary>
/// Financial-specific validation to prevent billing exploits.
/// Checks: negative balances, ValidFrom/ValidTo enforcement, transaction integrity.
/// </summary>
public interface IFinancialSafetyValidator
{
    /// <summary>
    /// Prevent player from having negative card balance (unlimited purchases)
    /// </summary>
    (bool isValid, string? error) ValidateCardBalance(decimal currentBalance, decimal chargeAmount);

    /// <summary>
    /// Validate card is within validity period before allowing access
    /// </summary>
    (bool isValid, string? error) ValidateCardValidity(DateTime validFrom, DateTime validTo, DateTime? checkTime = null);

    /// <summary>
    /// Ensure transaction has all required information (not NULL fields)
    /// </summary>
    (bool isValid, string? error) ValidateTransactionIntegrity(
        decimal amount,
        int? employeeId,
        string category,
        string description,
        string createdBy);

    /// <summary>
    /// Prevent subscription renewal exploit (from TODAY gives free month)
    /// </summary>
    (bool isValid, string? error) ValidateSubscriptionRenewal(
        DateTime currentEndDate,
        int renewalMonths,
        DateTime proposedNewEndDate);

    /// <summary>
    /// Comprehensive financial transaction validation
    /// </summary>
    (bool isValid, List<string> errors) ValidateFinancialTransaction(
        decimal amount,
        int? employeeId,
        string category,
        string description,
        bool deductFromBalance,
        decimal currentBalance);
}

public class FinancialSafetyValidator : IFinancialSafetyValidator
{
    public (bool isValid, string? error) ValidateCardBalance(decimal currentBalance, decimal chargeAmount)
    {
        // ❌ PREVENT: Player with negative balance buying more
        if (currentBalance < 0)
            return (false, $"Player cannot transact with negative balance ({currentBalance:C}). Must be settled first.");

        // ❌ PREVENT: Transaction would create negative balance
        if ((currentBalance - chargeAmount) < 0)
            return (false, $"Insufficient balance. Current: {currentBalance:C}, Required: {chargeAmount:C}");

        return (true, null);
    }

    public (bool isValid, string? error) ValidateCardValidity(DateTime validFrom, DateTime validTo, DateTime? checkTime = null)
    {
        var now = checkTime ?? DateTime.Today;

        // Card not yet valid
        if (now < validFrom)
            return (false, $"Card is not yet valid. Starts {validFrom:yyyy-MM-dd}");

        // Card expired
        if (now > validTo)
            return (false, $"Card has expired. Expired {validTo:yyyy-MM-dd}");

        return (true, null);
    }

    public (bool isValid, string? error) ValidateTransactionIntegrity(
        decimal amount,
        int? employeeId,
        string category,
        string description,
        string createdBy)
    {
        // ❌ PREVENT: Transaction.RelatedEmployeeId = NULL (can't reconcile)
        if (!employeeId.HasValue || employeeId.Value <= 0)
            return (false, "Transaction must be linked to an employee");

        if (string.IsNullOrWhiteSpace(category))
            return (false, "Transaction category cannot be empty");

        if (string.IsNullOrWhiteSpace(description))
            return (false, "Transaction description cannot be empty");

        if (string.IsNullOrWhiteSpace(createdBy))
            return (false, "Transaction must specify who created it");

        if (amount <= 0)
            return (false, "Transaction amount must be positive");

        return (true, null);
    }

    public (bool isValid, string? error) ValidateSubscriptionRenewal(
        DateTime currentEndDate,
        int renewalMonths,
        DateTime proposedNewEndDate)
    {
        // ❌ PREVENT: Renewal extends from TODAY instead of current EndDate
        // Should be: proposedNewEndDate = max(DateTime.Today, currentEndDate) + renewalMonths
        var correctNewEndDate = DateTime.Today > currentEndDate
            ? DateTime.Today.AddMonths(renewalMonths)
            : currentEndDate.AddMonths(renewalMonths);

        if (proposedNewEndDate != correctNewEndDate)
        {
            return (false,
                $"Incorrect renewal date. " +
                $"Current subscription ends {currentEndDate:yyyy-MM-dd}. " +
                $"New end date should be {correctNewEndDate:yyyy-MM-dd}, not {proposedNewEndDate:yyyy-MM-dd}. " +
                $"Renewal from wrong base date would give free subscription!");
        }

        return (true, null);
    }

    public (bool isValid, List<string> errors) ValidateFinancialTransaction(
        decimal amount,
        int? employeeId,
        string category,
        string description,
        bool deductFromBalance,
        decimal currentBalance)
    {
        var errors = new List<string>();

        var (integrityValid, integrityError) = ValidateTransactionIntegrity(
            amount, employeeId, category, description, "System");
        if (!integrityValid)
            errors.Add(integrityError!);

        if (deductFromBalance)
        {
            var (balanceValid, balanceError) = ValidateCardBalance(currentBalance, amount);
            if (!balanceValid)
                errors.Add(balanceError!);
        }

        return (errors.Count == 0, errors);
    }
}
