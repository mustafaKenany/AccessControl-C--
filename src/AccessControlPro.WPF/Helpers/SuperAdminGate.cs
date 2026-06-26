using System.Security.Cryptography;
using System.Text;

namespace AccessControlPro.WPF.Helpers;

/// <summary>
/// SuperAdmin step-up authentication. Reserved actions (enable POS, edit cloud settings, run data
/// migration, toggle online) require the SuperAdmin password on an OFFLINE machine. ONLINE gyms are
/// governed by the cloud feature flags instead, so the desktop usually won't even offer the action.
/// </summary>
public static class SuperAdminGate
{
    // SHA-256 of the SuperAdmin password — stored hashed, never as the literal string.
    private const string PasswordHash =
        "78FB4D7FB3455565B488C57666CDB469CAD5BEBCC282C7AD43C93F91E1F95174";

    public static bool Verify(string password)
    {
        if (string.IsNullOrEmpty(password)) return false;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        return string.Equals(hash, PasswordHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Prompts for the SuperAdmin password; returns true only if it's correct.</summary>
    public static bool RequireSuperAdmin(string actionDescription, System.Windows.Window? owner = null)
    {
        var dlg = new Views.SuperAdminPasswordDialog(actionDescription);
        if (owner != null) dlg.Owner = owner;
        return dlg.ShowDialog() == true;
    }
}
