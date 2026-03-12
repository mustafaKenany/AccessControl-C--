namespace AccessControlPro.Infrastructure.Validation;

/// <summary>
/// Universal validation for common data types.
/// Prevents invalid data like negative balances, huge heights, empty names, etc.
/// </summary>
public interface IUniversalDataValidator
{
    /// <summary>Validate name fields (not empty, reasonable length)</summary>
    (bool isValid, string? error) ValidateName(string? name, int minLength = 2, int maxLength = 100);

    /// <summary>Validate phone number format</summary>
    (bool isValid, string? error) ValidatePhone(string? phone);

    /// <summary>Validate decimal amounts (not negative, reasonable max)</summary>
    (bool isValid, string? error) ValidateCurrency(decimal amount, decimal minValue = 0, decimal maxValue = 1000000);

    /// <summary>Validate integer physical measurements (height, weight)</summary>
    (bool isValid, string? error) ValidatePhysicalMeasurement(int value, string fieldName, int minValue, int maxValue);

    /// <summary>Validate text field length</summary>
    (bool isValid, string? error) ValidateTextField(string? text, int maxLength, string fieldName);

    /// <summary>Validate date range (startDate before endDate)</summary>
    (bool isValid, string? error) ValidateDateRange(DateTime startDate, DateTime endDate, string? reason = null);

    /// <summary>Validate percentage (0-100)</summary>
    (bool isValid, string? error) ValidatePercentage(decimal value);

    /// <summary>Validate subscription months</summary>
    (bool isValid, string? error) ValidateSubscriptionPeriod(int months, int maxMonths = 120);

    /// <summary>Comprehensive validation for employee data</summary>
    (bool isValid, List<string> errors) ValidateEmployeeData(
        string fullNameEn,
        string? fullNameAr,
        string phone,
        int height,
        int weight,
        decimal subscriptionFee,
        decimal amountPaid,
        DateTime startDate,
        DateTime endDate);
}

public class UniversalDataValidator : IUniversalDataValidator
{
    public (bool isValid, string? error) ValidateName(string? name, int minLength = 2, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Name cannot be empty");

        if (name.Length < minLength)
            return (false, $"Name too short (minimum {minLength} characters)");

        if (name.Length > maxLength)
            return (false, $"Name too long (maximum {maxLength} characters)");

        // Check for invalid characters
        if (name.Any(c => char.IsControl(c)))
            return (false, "Name contains invalid characters");

        return (true, null);
    }

    public (bool isValid, string? error) ValidatePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return (false, "Phone number cannot be empty");

        if (phone.Length < 7 || phone.Length > 15)
            return (false, "Phone number must be between 7-15 digits");

        // Remove common separators and check if mostly digits
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
        if (digitsOnly.Length < 7)
            return (false, "Phone number must contain at least 7 digits");

        return (true, null);
    }

    public (bool isValid, string? error) ValidateCurrency(decimal amount, decimal minValue = 0, decimal maxValue = 1000000)
    {
        if (amount < minValue)
            return (false, $"Amount cannot be less than {minValue:C}");

        if (amount > maxValue)
            return (false, $"Amount cannot exceed {maxValue:C}");

        // Check for too many decimal places
        if (Math.Round(amount, 2) != amount)
            return (false, "Amount must have at most 2 decimal places");

        return (true, null);
    }

    public (bool isValid, string? error) ValidatePhysicalMeasurement(int value, string fieldName, int minValue, int maxValue)
    {
        if (value < minValue)
            return (false, $"{fieldName} cannot be less than {minValue}");

        if (value > maxValue)
            return (false, $"{fieldName} cannot exceed {maxValue}");

        return (true, null);
    }

    public (bool isValid, string? error) ValidateTextField(string? text, int maxLength, string fieldName)
    {
        if (text == null)
            return (true, null); // Optional field

        if (text.Length > maxLength)
            return (false, $"{fieldName} is too long (max {maxLength} characters, got {text.Length})");

        return (true, null);
    }

    public (bool isValid, string? error) ValidateDateRange(DateTime startDate, DateTime endDate, string? reason = null)
    {
        if (startDate > endDate)
            return (false, $"Start date must be before end date{(reason != null ? $" ({reason})" : "")}");

        // Maximum 10 years subscription
        if ((endDate - startDate).Days > 3650)
            return (false, "Date range too long (max 10 years)");

        return (true, null);
    }

    public (bool isValid, string? error) ValidatePercentage(decimal value)
    {
        if (value < 0 || value > 100)
            return (false, "Percentage must be between 0 and 100");

        return (true, null);
    }

    public (bool isValid, string? error) ValidateSubscriptionPeriod(int months, int maxMonths = 120)
    {
        if (months < 1)
            return (false, "Subscription period must be at least 1 month");

        if (months > maxMonths)
            return (false, $"Subscription period cannot exceed {maxMonths} months");

        return (true, null);
    }

    public (bool isValid, List<string> errors) ValidateEmployeeData(
        string fullNameEn,
        string? fullNameAr,
        string phone,
        int height,
        int weight,
        decimal subscriptionFee,
        decimal amountPaid,
        DateTime startDate,
        DateTime endDate)
    {
        var errors = new List<string>();

        var (nameValid, nameError) = ValidateName(fullNameEn, 2, 100);
        if (!nameValid) errors.Add(nameError!);

        if (!string.IsNullOrWhiteSpace(fullNameAr))
        {
            var (arNameValid, arNameError) = ValidateName(fullNameAr, 2, 100);
            if (!arNameValid) errors.Add($"Arabic name: {arNameError}");
        }

        var (phoneValid, phoneError) = ValidatePhone(phone);
        if (!phoneValid) errors.Add(phoneError!);

        // Height: 120-250 cm (4'- 8'3")
        var (heightValid, heightError) = ValidatePhysicalMeasurement(height, "Height", 120, 250);
        if (!heightValid) errors.Add(heightError!);

        // Weight: 25-200 kg
        var (weightValid, weightError) = ValidatePhysicalMeasurement(weight, "Weight", 25, 200);
        if (!weightValid) errors.Add(weightError!);

        var (feeValid, feeError) = ValidateCurrency(subscriptionFee, 0, 10000);
        if (!feeValid) errors.Add($"Subscription fee: {feeError}");

        var (paidValid, paidError) = ValidateCurrency(amountPaid, 0, 10000);
        if (!paidValid) errors.Add($"Amount paid: {paidError}");

        // Amount paid cannot exceed subscription fee (with 10% tolerance for overpayment)
        if (amountPaid > subscriptionFee * 1.1m)
            errors.Add($"Amount paid ({amountPaid}) cannot exceed subscription fee ({subscriptionFee})");

        var (dateValid, dateError) = ValidateDateRange(startDate, endDate, "for subscription");
        if (!dateValid) errors.Add(dateError!);

        return (errors.Count == 0, errors);
    }
}
