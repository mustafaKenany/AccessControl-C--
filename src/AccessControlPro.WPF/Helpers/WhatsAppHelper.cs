using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Builds and opens WhatsApp "renewal reminder" messages. No paid gateway — it opens
/// wa.me deep links in WhatsApp Desktop / WhatsApp Web with the message pre-filled, so the
/// cashier just hits send. Iraqi local numbers (07xx…) are converted to international
/// (9647xx…). The message template is editable and persisted per PC.
/// </summary>
public static class WhatsAppHelper
{
    private static readonly string TemplatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AccessControlPro", "whatsapp_reminder_template.txt");

    public static string DefaultTemplate => LanguageManager.Instance.IsArabic
        ? "مرحباً {name}، اشتراكك في {gym} ينتهي بتاريخ {date} (المتبقي {days} يوم). يرجى التجديد للاستمرار. شكراً 🙏"
        : "Hello {name}, your subscription at {gym} ends on {date} ({days} days left). Please renew to continue. Thank you 🙏";

    /// <summary>Load the saved template, or the localized default if none saved.</summary>
    public static string LoadTemplate()
    {
        try
        {
            if (File.Exists(TemplatePath))
            {
                var t = File.ReadAllText(TemplatePath);
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }
        }
        catch { }
        return DefaultTemplate;
    }

    public static void SaveTemplate(string template)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TemplatePath)!);
            File.WriteAllText(TemplatePath, template ?? "");
        }
        catch { }
    }

    /// <summary>Fill the template's placeholders for one member.</summary>
    public static string BuildMessage(string template, string name, DateTime endDate, int daysLeft)
    {
        return (template ?? DefaultTemplate)
            .Replace("{name}", name ?? "")
            .Replace("{gym}", GymProfile.DisplayName)
            .Replace("{date}", endDate.ToString("yyyy-MM-dd"))
            .Replace("{days}", daysLeft.ToString());
    }

    /// <summary>
    /// Convert an Iraqi local number to WhatsApp international form (digits only, 964…).
    /// "0771 234 5678" → "9647712345678". Returns "" if there aren't enough digits.
    /// </summary>
    public static string ToInternational(string phone)
    {
        var sb = new StringBuilder();
        foreach (var c in phone ?? "")
            if (char.IsDigit(c)) sb.Append(c);
        var d = sb.ToString();
        if (d.Length < 7) return "";

        if (d.StartsWith("00964")) d = d.Substring(2);       // 00964… → 964…
        else if (d.StartsWith("964")) { /* already intl */ }
        else if (d.StartsWith("0")) d = "964" + d.Substring(1); // local 07xx → 9647xx
        else d = "964" + d;                                   // bare 7xx → 9647xx
        return d;
    }

    /// <summary>Open WhatsApp with the message pre-filled for this phone. Returns false if the number is unusable.</summary>
    public static bool OpenChat(string phone, string message)
    {
        var intl = ToInternational(phone);
        if (string.IsNullOrEmpty(intl)) return false;
        var url = $"https://wa.me/{intl}?text={Uri.EscapeDataString(message ?? "")}";
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }
}
