namespace AccessControlPro.Web.Data;

/// <summary>
/// All timestamps in the cloud DB are stored in UTC. Every gym is in Iraq, which is a fixed
/// UTC+3 (no DST since 2008), and the VPS itself runs in UTC — so the old <c>.ToLocalTime()</c>
/// calls were a no-op (Npgsql returns Kind=Unspecified) and the portal showed raw UTC, 3 hours
/// behind local time. Use <c>.ToIraqTime()</c> at display sites to convert explicitly.
/// </summary>
public static class TimeExtensions
{
    public static DateTime ToIraqTime(this DateTime utc) => utc.AddHours(3);
}
