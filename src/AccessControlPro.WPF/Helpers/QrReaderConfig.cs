using System.IO;
using System.Text.Json;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Some QR readers deliver the scanned value bit-shifted — e.g. they drop the low hex digit, so the
/// controller receives value ÷ 16. To compensate without touching the reader, the QR pool stores the
/// DEVICE value (what the gate holds + reports in events) and the printed QR encodes value × divisor,
/// so the reader's division lands exactly back on the stored value.
///
/// Configured per install via "QrReaderDivisor" in appsettings.json. Default 1 = no transformation
/// (readers that pass the value through unchanged — i.e. every other gym — are unaffected).
/// </summary>
public static class QrReaderConfig
{
    private static int? _cached;

    public static int Divisor
    {
        get
        {
            if (_cached.HasValue) return _cached.Value;
            int div = 1;
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (File.Exists(path))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    if (doc.RootElement.TryGetProperty("QrReaderDivisor", out var el)
                        && el.TryGetInt32(out var v) && v >= 1)
                        div = v;
                }
            }
            catch { /* default 1 */ }
            _cached = div;
            return div;
        }
    }

    /// <summary>The value to ENCODE in the QR image for a given stored device code.</summary>
    public static string QrValueFor(string deviceCode)
    {
        var div = Divisor;
        if (div > 1 && long.TryParse(deviceCode, out var c))
            return (c * div).ToString();
        return deviceCode;
    }
}
