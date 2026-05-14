using AccessControlPro.Web.Data;

namespace AccessControlPro.Web.Middleware;

/// <summary>
/// Resolves which gym a request is for, based on the URL subdomain.
///
/// Rules:
///   basmia.hmtech.solutions   → look up gym with Subdomain="basmia"
///   admin.hmtech.solutions    → reserved for SuperAdmin, no gym attached
///   www.hmtech.solutions      → reserved (treated as root)
///   hmtech.solutions          → root domain (landing page, /api/sync via ApiKey)
///   localhost                 → development root
///   anything.localhost        → development subdomain (e.g. basmia.localhost via hosts file)
///
/// If a gym is resolved, it is placed in HttpContext.Items["CurrentGym"] as a
/// <see cref="GymInfo"/> so downstream pages/services can read it via the
/// <see cref="GymContextAccessor"/> extension methods on HttpContext.
/// </summary>
public class GymSubdomainMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Hosts reserved for non-gym purposes (root/admin/etc).</summary>
    private static readonly HashSet<string> ReservedSubdomains =
        new(StringComparer.OrdinalIgnoreCase) { "www", "admin", "api", "" };

    public GymSubdomainMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, GymDbHelper gymDb)
    {
        var subdomain = ExtractSubdomain(context.Request.Host.Host);

        if (!string.IsNullOrEmpty(subdomain) && !ReservedSubdomains.Contains(subdomain))
        {
            try
            {
                var gym = await gymDb.ResolveGymBySubdomainAsync(subdomain);
                if (gym != null)
                {
                    context.Items["CurrentGym"] = gym;
                }
            }
            catch
            {
                // Master DB unreachable — let the request proceed without a resolved gym.
                // The login page will surface the situation to the user.
            }
        }

        // Flag the special "admin" subdomain so SuperAdmin routing can use it.
        if (string.Equals(subdomain, "admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Items["IsSuperAdminHost"] = true;
        }

        await _next(context);
    }

    /// <summary>
    /// Strip the registrable domain ("hmtech.solutions", "localhost") from a Host header
    /// value and return the leftmost remaining label. Examples:
    ///   "basmia.hmtech.solutions" → "basmia"
    ///   "hmtech.solutions"        → "" (root)
    ///   "basmia.localhost"        → "basmia" (dev)
    ///   "localhost"               → "" (dev root)
    ///   "89.116.39.155"           → "" (raw IP — pre-DNS testing)
    /// </summary>
    private static string ExtractSubdomain(string host)
    {
        if (string.IsNullOrEmpty(host)) return "";

        // Raw IP — no subdomain concept
        if (System.Net.IPAddress.TryParse(host, out _)) return "";

        var parts = host.Split('.');

        // localhost dev case: "basmia.localhost" (2 parts) or just "localhost" (1 part)
        if (parts.Length == 2 && parts[1].Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return parts[0];
        if (parts.Length == 1) return "";

        // Production: anything before the registrable domain (last 2 labels: "hmtech.solutions")
        if (parts.Length >= 3)
            return parts[0];

        // Exactly 2 parts like "hmtech.solutions" — root, no subdomain
        return "";
    }
}

/// <summary>
/// Convenience extension methods to read the resolved gym from any code that has access
/// to HttpContext (Blazor pages via IHttpContextAccessor, endpoint handlers, etc.).
/// </summary>
public static class GymContextAccessor
{
    public static GymInfo? GetCurrentGym(this HttpContext? context)
        => context?.Items.TryGetValue("CurrentGym", out var v) == true ? v as GymInfo : null;

    public static bool IsSuperAdminHost(this HttpContext? context)
        => context?.Items.TryGetValue("IsSuperAdminHost", out var v) == true
            && v is bool b && b;
}
