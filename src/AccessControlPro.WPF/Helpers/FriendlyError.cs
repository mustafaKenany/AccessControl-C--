using System;
using System.IO;
using AccessControlPro.Application.Services;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// Turns a raw exception into a short, calm, bilingual message an ordinary gym owner can read —
/// never an English stack trace, a file path, or a scary word like "Exception"/"Assertion". Every
/// message ends on a reassuring note ("you can keep working"), and — when the gym is online — says
/// the company was told automatically, so the operator doesn't panic or feel they must call.
/// This is the wall in front of the last-resort crash dialog (App.DispatcherUnhandledException).
/// </summary>
public static class FriendlyError
{
    public static (string Title, string Body) Describe(Exception? ex, bool reportedToCompany)
    {
        bool ar = LanguageManager.Instance.IsArabic;

        // Database / network — the most common real cause the operator can sometimes fix themselves.
        if (ex != null && DbConnectionHelper.IsConnectionError(ex))
        {
            return ar
                ? ("تعذّر الاتصال",
                   "انقطع الاتصال بقاعدة البيانات مؤقتاً. تأكد من تشغيل الجهاز والشبكة. " +
                   "غالباً يكفي إغلاق البرنامج وفتحه من جديد." + Tail(ar, reportedToCompany))
                : ("Connection problem",
                   "The connection to the database dropped for a moment. Please check the server and network are on. " +
                   "Closing and reopening the app usually fixes it." + Tail(ar, reportedToCompany));
        }

        // Disk / file trouble (full disk, locked file, missing path).
        if (ex is IOException || ex is UnauthorizedAccessException)
        {
            return ar
                ? ("مشكلة في الحفظ",
                   "واجه البرنامج صعوبة في قراءة أو حفظ ملف. تأكد من وجود مساحة كافية على القرص." + Tail(ar, reportedToCompany))
                : ("Storage problem",
                   "The app had trouble reading or saving a file. Please make sure there is free space on the disk." + Tail(ar, reportedToCompany));
        }

        // Anything else — keep it vague, calm, and non-technical on purpose.
        return ar
            ? ("تنبيه بسيط",
               "حدث خطأ بسيط ولم يتأثر عملك أو بياناتك. يمكنك متابعة الاستخدام بشكل طبيعي." + Tail(ar, reportedToCompany))
            : ("Small hiccup",
               "A small error occurred. Your work and data are safe — you can keep using the app normally." + Tail(ar, reportedToCompany));
    }

    private static string Tail(bool ar, bool reportedToCompany)
    {
        if (!reportedToCompany) return "";
        return ar
            ? "\n\nتم إبلاغ الشركة تلقائياً وسنعالج الأمر."
            : "\n\nThe company was notified automatically and will look into it.";
    }
}
