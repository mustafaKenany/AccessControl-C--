namespace AccessControlPro.Infrastructure.DeviceManagement;

/// <summary>
/// Validates card DoorPermissions format before sending to SDK.
/// Prevents crashes from invalid permission strings.
/// Door permissions format: "01000000" where each bit represents a door (1=allowed, 0=denied)
/// </summary>
public interface ICardDoorPermissionsValidator
{
    /// <summary>
    /// Validates door permissions format. Should be 8-char binary string.
    /// Returns (isValid, errorMessage)
    /// </summary>
    (bool isValid, string? error) ValidateDoorPermissions(string doorPermissions);

    /// <summary>
    /// Validates EffectiveTimes (usage count). Valid: 1-65535 or 0 (unlimited)
    /// </summary>
    (bool isValid, string? error) ValidateEffectiveTimes(int effectiveTimes);

    /// <summary>
    /// Validates card validity dates. ValidFrom must be before ValidTo.
    /// </summary>
    (bool isValid, string? error) ValidateCardValidity(DateTime validFrom, DateTime validTo);

    /// <summary>
    /// Validates OpenMode parameter (SDK specific)
    /// </summary>
    (bool isValid, string? error) ValidateOpenMode(int openMode);

    /// <summary>
    /// Comprehensive card parameters validation
    /// </summary>
    (bool isValid, List<string> errors) ValidateCardParameters(
        string cardNumber,
        string doorPermissions,
        int effectiveTimes,
        int openMode,
        DateTime validFrom,
        DateTime validTo);
}

public class CardDoorPermissionsValidator : ICardDoorPermissionsValidator
{
    private const int DoorPermissionLength = 8;
    private const int MinEffectiveTimes = 0;
    private const int MaxEffectiveTimes = 65535;
    private const int ValidOpenModeMin = 0;
    private const int ValidOpenModeMax = 3; // Depends on hardware, adjust as needed

    public (bool isValid, string? error) ValidateDoorPermissions(string doorPermissions)
    {
        if (string.IsNullOrWhiteSpace(doorPermissions))
            return (false, "Door permissions cannot be empty.");

        // Should be exactly 8 characters
        if (doorPermissions.Length != DoorPermissionLength)
            return (false, $"Door permissions must be exactly {DoorPermissionLength} characters (got {doorPermissions.Length}).");

        // Should only contain 0 and 1
        if (!doorPermissions.All(c => c == '0' || c == '1'))
            return (false, $"Door permissions must contain only '0' and '1' digits (got '{doorPermissions}').");

        return (true, null);
    }

    public (bool isValid, string? error) ValidateEffectiveTimes(int effectiveTimes)
    {
        if (effectiveTimes < MinEffectiveTimes || effectiveTimes > MaxEffectiveTimes)
            return (false, $"EffectiveTimes must be between {MinEffectiveTimes} and {MaxEffectiveTimes} (got {effectiveTimes}).");

        return (true, null);
    }

    public (bool isValid, string? error) ValidateCardValidity(DateTime validFrom, DateTime validTo)
    {
        // ValidFrom should be in past or near future (within 7 days)
        if (validFrom > DateTime.Today.AddDays(7))
            return (false, $"ValidFrom date is too far in future: {validFrom:yyyy-MM-dd}");

        // ValidTo should be after ValidFrom
        if (validTo <= validFrom)
            return (false, $"ValidTo ({validTo:yyyy-MM-dd}) must be after ValidFrom ({validFrom:yyyy-MM-dd}).");

        // Card should not be valid for more than 10 years (sanity check)
        if ((validTo - validFrom).Days > 3650)
            return (false, $"Card validity period too long: {(validTo - validFrom).Days} days. Max 10 years.");

        return (true, null);
    }

    public (bool isValid, string? error) ValidateOpenMode(int openMode)
    {
        if (openMode < ValidOpenModeMin || openMode > ValidOpenModeMax)
            return (false, $"OpenMode must be between {ValidOpenModeMin} and {ValidOpenModeMax} (got {openMode}).");

        return (true, null);
    }

    public (bool isValid, List<string> errors) ValidateCardParameters(
        string cardNumber,
        string doorPermissions,
        int effectiveTimes,
        int openMode,
        DateTime validFrom,
        DateTime validTo)
    {
        var errors = new List<string>();

        // Validate card number
        if (string.IsNullOrWhiteSpace(cardNumber))
            errors.Add("Card number cannot be empty.");
        else if (cardNumber.Length > 50)
            errors.Add($"Card number too long: {cardNumber.Length} characters");

        // Validate door permissions
        var (permValid, permError) = ValidateDoorPermissions(doorPermissions);
        if (!permValid) errors.Add(permError!);

        // Validate effective times
        var (timesValid, timesError) = ValidateEffectiveTimes(effectiveTimes);
        if (!timesValid) errors.Add(timesError!);

        // Validate open mode
        var (modeValid, modeError) = ValidateOpenMode(openMode);
        if (!modeValid) errors.Add(modeError!);

        // Validate dates
        var (datesValid, datesError) = ValidateCardValidity(validFrom, validTo);
        if (!datesValid) errors.Add(datesError!);

        return (errors.Count == 0, errors);
    }
}
