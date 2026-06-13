namespace AccessControlPro.Web.Services;

/// <summary>
/// Some audit-log details and delete reasons were stored as a single "English / Arabic"
/// string. These helpers return only the half matching the active UI language, so the
/// portal never shows both languages merged in one cell.
/// </summary>
public static class DualLanguageText
{
    public static string Single(string? text, bool isArabic)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        var idx = text.IndexOf(" / ", System.StringComparison.Ordinal);
        if (idx < 0) return text;

        var first = text.Substring(0, idx).Trim();
        var second = text.Substring(idx + 3).Trim();
        bool firstAr = HasArabic(first);
        bool secondAr = HasArabic(second);

        // Only split when one side is Arabic and the other is not — otherwise it's a normal
        // string that happens to contain " / " and must be left untouched.
        if (secondAr && !firstAr) return isArabic ? second : first;
        if (firstAr && !secondAr) return isArabic ? first : second;
        return text;
    }

    private static bool HasArabic(string s)
    {
        foreach (var c in s)
            if (c >= 0x0600 && c <= 0x06FF) return true;
        return false;
    }
}
