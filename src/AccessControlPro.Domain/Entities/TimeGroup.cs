namespace AccessControlPro.Domain.Entities;

/// <summary>
/// Defines a time-based access schedule that can be assigned to cards.
/// Maps to hardware time zones (1-64) on the access control device.
/// </summary>
public class TimeGroup
{
    public int Id { get; set; }

    /// <summary>Name displayed in UI (e.g., "Morning Gym", "Friday Only")</summary>
    public string NameEn { get; set; } = "";
    public string NameAr { get; set; } = "";

    /// <summary>Hardware time zone index (1-64). Must be unique per device.</summary>
    public int HardwareIndex { get; set; } = 1;

    /// <summary>Is this the default "24/7" group?</summary>
    public bool IsDefault { get; set; }

    /// <summary>JSON string storing the weekly schedule. Format:
    /// {"Mon":["09:00-13:00","17:00-22:00"],"Tue":["09:00-13:00"],...}
    /// Empty object {} = 24/7 full access.
    /// Empty array for a day = closed that day.
    /// </summary>
    public string ScheduleJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
