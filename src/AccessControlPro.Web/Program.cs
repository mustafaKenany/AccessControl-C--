using AccessControlPro.Web.Components;
using AccessControlPro.Web.Data;
using AccessControlPro.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// CloudConnection must be provided via appsettings.json, an appsettings.{Environment}.json,
// environment variables (ConnectionStrings__CloudConnection), or CLI args. Failing loud
// here is better than falling back to a hardcoded password that would ship in source.
var cloudConn = builder.Configuration.GetConnectionString("CloudConnection")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:CloudConnection is not configured. " +
        "Set it in appsettings.json or via the ConnectionStrings__CloudConnection environment variable.");

builder.Services.AddSingleton(new DbHelper(cloudConn));
builder.Services.AddSingleton(new GymDbHelper(cloudConn));
builder.Services.AddSingleton<WebAuthService>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddScoped<SessionState>();
builder.Services.AddHttpContextAccessor();

// Web push (member renewal reminders). PushService is a no-op until VAPID keys are
// set in config; the hosted service sweeps gyms twice a day for expiring members.
builder.Services.AddSingleton<PushService>();
builder.Services.AddHostedService<PushReminderHostedService>();

// Allow large request bodies for sync API (50MB)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50_000_000;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50_000_000;
});

builder.WebHost.UseUrls("http://0.0.0.0:5000");

// Trust forwarded headers from a reverse proxy (nginx/Caddy/etc. in front of the Kestrel
// listener). Without this, Blazor thinks every request is plain HTTP — which makes
// UseHttpsRedirection misbehave and means cookies set with Request.IsHttps-gated flags
// won't get the Secure attribute. Limit to known proxy ranges if you have them; for
// single-VPS deploys, accepting any forwarded proxy is fine.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Must come before any middleware that reads the scheme/host.
app.UseForwardedHeaders();

// Shared API-key validator used by every /api/* endpoint. The inline version had a
// subtle bypass: `null == null` was true, so a missing X-Api-Key header + unset
// SyncApiKey config authenticated anyone. Centralizing the check means future
// endpoints can't drift back into that bug.
async Task<bool> IsApiKeyValidAsync(HttpContext ctx, GymDbHelper gymDb)
{
    var key = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(key)) return false;

    var configuredKey = app.Configuration["SyncApiKey"];
    if (!string.IsNullOrEmpty(configuredKey) && key == configuredKey) return true;

    return !string.IsNullOrEmpty(await gymDb.GetDatabaseByApiKeyAsync(key));
}

// Initialize database tables
try
{
    var db = app.Services.GetRequiredService<DbHelper>();
    await db.InitializeDatabaseAsync();
    Console.WriteLine("Database initialized successfully.");
}
catch (Exception ex)
{
    Console.WriteLine($"Database init error: {ex.Message}");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseResponseCompression();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";
        // Versioned libraries, fonts, and images rarely change → cache 30 days so a
        // returning owner's phone loads them from cache instead of re-fetching.
        // The app's own CSS/JS stay short (5 min) so a deploy is never served stale.
        bool longLived =
            path.StartsWith("/lib/") || path.StartsWith("/_content/") ||
            path.EndsWith(".woff2") || path.EndsWith(".woff") || path.EndsWith(".ttf") ||
            path.EndsWith(".png") || path.EndsWith(".jpg") || path.EndsWith(".jpeg") ||
            path.EndsWith(".svg") || path.EndsWith(".ico") || path.EndsWith(".webp");
        ctx.Context.Response.Headers["Cache-Control"] =
            longLived ? "public,max-age=2592000" : "public,max-age=300";
    }
});
app.UseAntiforgery();

// Lightweight liveness/readiness probe for uptime monitoring and the reverse proxy.
// Returns 200 only if the master DB answers; 503 otherwise. No auth, no tenant lookup.
app.MapGet("/health", async (GymDbHelper gymDb) =>
{
    try
    {
        using var conn = await gymDb.GetMasterConnectionAsync();
        using var cmd = new Npgsql.NpgsqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync();
        return Results.Ok(new { status = "ok" });
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

// Resolve which gym (tenant) the request is for, based on the URL subdomain.
// Must run BEFORE the session reader so login pages can know which gym DB to
// authenticate against. Reserved subdomains (www, admin, api) are skipped.
app.UseMiddleware<AccessControlPro.Web.Middleware.GymSubdomainMiddleware>();

// Session-reader middleware: validates the cookie and populates HttpContext.Items
// so Blazor components can read the session in OnInitialized. Placed AFTER
// UseStaticFiles so CSS/JS/images don't trigger DB lookups. Sync API endpoints
// use X-Api-Key instead so they'll just fall through (no cookie = no DB hit).
app.Use(async (context, next) =>
{
    var token = context.Request.Cookies[SessionService.CookieName];
    if (!string.IsNullOrWhiteSpace(token))
    {
        var sessionService = context.RequestServices.GetRequiredService<SessionService>();
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var ua = context.Request.Headers.UserAgent.ToString();
        var info = await sessionService.ValidateAsync(token, ip, ua);
        if (info != null)
        {
            context.Items["SessionInfo"] = info;
        }
        else
        {
            // Token invalid/expired/revoked — tell the browser to stop sending it.
            context.Response.Cookies.Delete(SessionService.CookieName);
        }
    }
    await next();
});

// Sync API — receives data from WPF app via HTTPS
app.MapPost("/api/sync", async (HttpContext context, DbHelper db, GymDbHelper gymDb) =>
{
    // Each request must start with a fresh error list (AsyncLocal) — otherwise concurrent
    // requests from different gyms would share accumulated errors via the old static field.
    SyncHelper.ResetErrors();

    if (!await IsApiKeyValidAsync(context, gymDb))
        return Results.Unauthorized();

    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();

    try
    {
        // Support gzip-compressed request bodies. Cap decompressed size at 200 MB so a
        // malformed or malicious request can't zip-bomb the server into OOM.
        const long MaxDecompressedBytes = 200 * 1024 * 1024;
        string json;
        if (context.Request.Headers.ContentEncoding.ToString().Contains("gzip"))
        {
            using var decompressed = new System.IO.MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(context.Request.Body, System.IO.Compression.CompressionMode.Decompress))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int read;
                while ((read = await gzip.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    totalRead += read;
                    if (totalRead > MaxDecompressedBytes)
                        return Results.BadRequest($"Payload exceeds {MaxDecompressedBytes / 1024 / 1024} MB decompressed limit");
                    await decompressed.WriteAsync(buffer.AsMemory(0, read));
                }
            }
            json = System.Text.Encoding.UTF8.GetString(decompressed.ToArray());
        }
        else
        {
            using var reader = new StreamReader(context.Request.Body);
            json = await reader.ReadToEndAsync();
            if (json.Length > MaxDecompressedBytes)
                return Results.BadRequest($"Payload exceeds {MaxDecompressedBytes / 1024 / 1024} MB limit");
        }
        var syncData = System.Text.Json.JsonSerializer.Deserialize<SyncPayload>(json);

        if (syncData == null)
            return Results.BadRequest("Invalid sync data");

        // Find the gym's database by API key
        var dbName = await gymDb.GetDatabaseByApiKeyAsync(apiKey ?? "");
        if (string.IsNullOrEmpty(dbName))
        {
            // Fallback for backward compatibility (single gym)
            dbName = "gymcloud";
        }

        // Sync mode: "full" wipes tables before insert (initial sync or manual reset);
        // "delta" only upserts changed rows (skips the DELETE). Default "full" preserves
        // behavior for older clients that don't send the header.
        var syncMode = context.Request.Headers["X-Sync-Mode"].FirstOrDefault() ?? "full";
        var isFullSync = !string.Equals(syncMode, "delta", StringComparison.OrdinalIgnoreCase);

        using var conn = await gymDb.GetGymConnectionAsync(dbName);
        int total = 0;

        // Process each table — always call even with 0 rows (to clear cloud when local is empty in full-sync mode)
        if (syncData.Players != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "Players", syncData.Players, isFullSync);
        if (syncData.AccessEvents != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "AccessEvents", syncData.AccessEvents, isFullSync);
        if (syncData.Devices != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "Devices", syncData.Devices, isFullSync);
        if (syncData.Doors != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "Doors", syncData.Doors, isFullSync);
        if (syncData.Transactions != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "Transactions", syncData.Transactions, isFullSync);
        if (syncData.Users != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "Users", syncData.Users, isFullSync);
        if (syncData.AuditLogs != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "AuditLogs", syncData.AuditLogs, isFullSync);
        if (syncData.DeletedEmployees != null)
        {
            total += await SyncHelper.UpsertRowsAsync(conn, "DeletedEmployees", syncData.DeletedEmployees, isFullSync);
            // Propagate deletes: for each tombstone row, remove the matching Players row
            // (only meaningful in delta mode — full sync already deleted Players before inserting)
            if (!isFullSync)
                await SyncHelper.ApplyDeleteTombstonesAsync(conn, "Players", "OriginalId", syncData.DeletedEmployees);
        }
        if (syncData.AppSettings != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "AppSettings", syncData.AppSettings, isFullSync);
        if (syncData.AccessCards != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "AccessCards", syncData.AccessCards, isFullSync);
        if (syncData.QrPool != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "QrPool", syncData.QrPool, isFullSync);
        if (syncData.SubscriptionPlans != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "SubscriptionPlans", syncData.SubscriptionPlans, isFullSync);
        if (syncData.PosShifts != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "PosShifts", syncData.PosShifts, isFullSync);
        if (syncData.FreezeHistories != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "FreezeHistories", syncData.FreezeHistories, isFullSync);
        if (syncData.Products != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "Products", syncData.Products, isFullSync);
        if (syncData.TimeGroups != null)
            total += await SyncHelper.UpsertRowsAsync(conn, "TimeGroups", syncData.TimeGroups, isFullSync);

        // Invalidate cached data after sync
        QueryCache.InvalidateAll();

        // Get errors from SyncHelper
        var syncErrors = SyncHelper.GetLastErrors();
        var status = syncErrors.Count == 0 ? "Success" : "PartialSuccess";
        var details = $"Synced {total} records via API";
        if (syncErrors.Count > 0)
            details += " | Errors: " + string.Join("; ", syncErrors.Take(10));

        // Log sync in the gym's database
        try
        {
            using var logCmd = new Npgsql.NpgsqlCommand(
                @"INSERT INTO ""CloudSyncLogs"" (""SyncType"", ""Status"", ""Details"", ""SyncedAt"")
                  VALUES ('API', @s, @d, @ts)", conn);
            logCmd.Parameters.AddWithValue("s", status);
            logCmd.Parameters.AddWithValue("d", details);
            logCmd.Parameters.AddWithValue("ts", DateTime.UtcNow);
            await logCmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { Console.WriteLine($"[Program] SyncLogInsert Error: {ex.Message}"); }

        // Update gym's LastSyncAt and PlayerCount in master database
        try
        {
            var playerCount = syncData.Players?.Count ?? 0;
            using var masterConn = await gymDb.GetMasterConnectionAsync();
            using var updateCmd = new Npgsql.NpgsqlCommand(
                @"UPDATE ""Gyms"" SET ""LastSyncAt"" = @ts, ""PlayerCount"" = @pc
                  WHERE ""ApiKey"" = @key", masterConn);
            updateCmd.Parameters.AddWithValue("ts", DateTime.UtcNow);
            updateCmd.Parameters.AddWithValue("pc", playerCount);
            updateCmd.Parameters.AddWithValue("key", apiKey);
            await updateCmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { Console.WriteLine($"[Program] UpdateGymSync Error: {ex.Message}"); }

        return Results.Ok(new { success = true, total, errors = syncErrors });
    }
    catch (Exception ex)
    {
        // Log full details server-side; return generic message so DB schema / paths / etc.
        // don't leak to clients via exception text.
        Console.WriteLine($"[api/sync] Exception: {ex}");
        return Results.Problem("Sync failed");
    }
});

// Pull users API — local app pulls new users from cloud
app.MapGet("/api/users", async (HttpContext context, GymDbHelper gymDb, DbHelper db) =>
{
    if (!await IsApiKeyValidAsync(context, gymDb))
        return Results.Unauthorized();

    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();

    try
    {
        // Find the gym's database by API key
        var dbName = await gymDb.GetDatabaseByApiKeyAsync(apiKey ?? "");
        if (string.IsNullOrEmpty(dbName))
        {
            // Fallback for backward compatibility (single gym)
            dbName = "gymcloud";
        }

        using var conn = await gymDb.GetGymConnectionAsync(dbName);
        using var cmd = new Npgsql.NpgsqlCommand(
            @"SELECT ""Id"", ""Username"", ""PasswordHash"", ""DisplayName"", ""Role"", ""IsActive"", ""Permissions""
              FROM ""Users"" ORDER BY ""Id""", conn);

        var users = new List<Dictionary<string, object?>>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            users.Add(new Dictionary<string, object?>
            {
                ["Id"] = reader.GetInt32(0),
                ["Username"] = reader.GetString(1),
                ["PasswordHash"] = reader.GetString(2),
                ["DisplayName"] = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ["Role"] = reader.IsDBNull(4) ? "User" : reader.GetString(4),
                ["IsActive"] = reader.GetBoolean(5),
                ["Permissions"] = reader.IsDBNull(6) ? "" : reader.GetString(6)
            });
        }

        return Results.Ok(users);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[api/users] Exception: {ex}");
        return Results.Problem("Failed to get users");
    }
});

// QR Pool API — local app pulls cloud-assigned QR codes
app.MapGet("/api/qr-pool", async (HttpContext context, GymDbHelper gymDb) =>
{
    if (!await IsApiKeyValidAsync(context, gymDb))
        return Results.Unauthorized();

    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();

    try
    {
        var dbName = await gymDb.GetDatabaseByApiKeyAsync(apiKey ?? "");
        if (string.IsNullOrEmpty(dbName)) dbName = "gymcloud";

        using var conn = await gymDb.GetGymConnectionAsync(dbName);
        using var cmd = new Npgsql.NpgsqlCommand(
            @"SELECT ""Code"", ""Status"", ""Source"", ""GuestName"", ""GuestPhone"", ""Reason"",
                     ""MaxUses"", ""UsedCount"", ""ValidTo"", ""DoorPermissions"", ""AssignedAt""
              FROM ""QrPool"" WHERE ""Status"" = 1 AND ""Source"" = 'Cloud'", conn);

        var entries = new List<Dictionary<string, object?>>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new Dictionary<string, object?>
            {
                ["Code"] = reader.GetString(0),
                ["Status"] = reader.GetInt32(1),
                ["Source"] = reader.GetString(2),
                ["GuestName"] = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ["GuestPhone"] = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ["Reason"] = reader.IsDBNull(5) ? "" : reader.GetString(5),
                ["MaxUses"] = reader.GetInt32(6),
                ["UsedCount"] = reader.GetInt32(7),
                ["ValidTo"] = reader.GetDateTime(8),
                ["DoorPermissions"] = reader.IsDBNull(9) ? "01010000" : reader.GetString(9),
                ["AssignedAt"] = reader.IsDBNull(10) ? null : (object)reader.GetDateTime(10)
            });
        }

        return Results.Ok(entries);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[api/qr-pool] Exception: {ex}");
        return Results.Problem("Failed to get QR pool");
    }
});

// Sync control API — client polls this before sync. If the gym's ForceFullSync flag is set,
// the response tells the client to reset its delta state and do a full sync. This is a
// "read-and-clear" operation so the flag only triggers once per click.
app.MapGet("/api/sync-control", async (HttpContext context, DbHelper db, GymDbHelper gymDb) =>
{
    if (!await IsApiKeyValidAsync(context, gymDb))
        return Results.Unauthorized();

    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();

    try
    {
        using var masterConn = await gymDb.GetMasterConnectionAsync();
        // Atomic read-and-clear: RETURNING gives us the previous value in a single UPDATE.
        using var cmd = new Npgsql.NpgsqlCommand(
            @"UPDATE ""Gyms"" SET ""ForceFullSync"" = FALSE
              WHERE ""ApiKey"" = @key AND ""ForceFullSync"" = TRUE
              RETURNING ""Id""", masterConn);
        cmd.Parameters.AddWithValue("key", apiKey ?? "");
        var result = await cmd.ExecuteScalarAsync();
        var forceFullSync = result != null; // a row was returned = flag was TRUE, now cleared

        // Remote lock status (payment enforcement) — read each poll so unlocks apply fast.
        bool locked = false;
        string lockMessage = "";
        using (var lockCmd = new Npgsql.NpgsqlCommand(
            @"SELECT COALESCE(""IsLocked"", FALSE), COALESCE(""LockMessage"", '')
              FROM ""Gyms"" WHERE ""ApiKey"" = @key", masterConn))
        {
            lockCmd.Parameters.AddWithValue("key", apiKey ?? "");
            using var r = await lockCmd.ExecuteReaderAsync();
            if (await r.ReadAsync()) { locked = r.GetBoolean(0); lockMessage = r.GetString(1); }
        }

        return Results.Ok(new { forceFullSync, locked, lockMessage });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[api/sync-control] Exception: {ex}");
        return Results.Problem("Sync control error");
    }
});

// Read-only remote-lock status (no side effects) — polled by the desktop app to enforce
// the payment lock independently of the sync flow (which read-and-clears ForceFullSync).
app.MapGet("/api/lock-status", async (HttpContext context, GymDbHelper gymDb) =>
{
    if (!await IsApiKeyValidAsync(context, gymDb))
        return Results.Unauthorized();

    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
    try
    {
        using var conn = await gymDb.GetMasterConnectionAsync();
        using var cmd = new Npgsql.NpgsqlCommand(
            @"SELECT COALESCE(""IsLocked"", FALSE), COALESCE(""LockMessage"", ''),
                     COALESCE(""OnlineEnabled"", TRUE), COALESCE(""PosEnabled"", FALSE)
              FROM ""Gyms"" WHERE ""ApiKey"" = @key", conn);
        cmd.Parameters.AddWithValue("key", apiKey ?? "");
        using var r = await cmd.ExecuteReaderAsync();
        bool locked = false; string msg = ""; bool onlineEnabled = true; bool posEnabled = false;
        if (await r.ReadAsync())
        {
            locked = r.GetBoolean(0); msg = r.GetString(1);
            onlineEnabled = r.GetBoolean(2); posEnabled = r.GetBoolean(3);
        }
        // SuperAdmin-reserved feature flags ride the same status poll the desktop already makes.
        return Results.Ok(new { locked, lockMessage = msg, onlineEnabled, posEnabled });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[api/lock-status] {ex}");
        return Results.Problem("lock-status error");
    }
});

// === Auth endpoints (server-side cookie + session store) ============================
// These issue HttpOnly cookies and manage the Sessions table. Blazor components POST
// here to establish/tear down the session; no session info is ever visible to JS.

static CookieOptions BuildSessionCookie(HttpContext ctx, TimeSpan lifetime)
{
    return new CookieOptions
    {
        HttpOnly = true,
        Secure = ctx.Request.IsHttps, // ForwardedHeaders ensures this reflects the proxy's scheme
        // Lax (not Strict): a Strict cookie is withheld on an installed PWA's cold-launch
        // navigation to its start_url (no same-site context), so the app opened to a stuck
        // spinner / login loop while a normal browser tab worked. Lax still sends on
        // top-level GET navigations and remains CSRF-safe for the POST sync/login endpoints.
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.Add(lifetime),
        Path = "/"
    };
}

// Auto-provision a gym from the desktop setup wizard: creates the tenant DB (with full
// schema via CreateGymDatabaseAsync) AND the master Gyms row in one call, gated by a
// provisioning password. Idempotent by API key so re-running setup is safe. Existing gyms
// and manual SuperAdmin creation are untouched. Offline installs simply skip this call.
app.MapPost("/api/provision-gym", async (HttpContext ctx, DbHelper db, GymDbHelper gymDb, IConfiguration config, WebAuthService auth) =>
{
    // Brute-force protection on the provisioning secret (5 attempts / 15 min per IP, like login).
    var pip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var pKey = $"provision:{pip}";
    if (auth.IsLoginBlocked(pKey))
        return Results.Json(new { success = false, error = "Too many attempts. Try again in 15 minutes." }, statusCode: 429);

    using var doc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
    var root = doc.RootElement;
    string S(string n) => root.TryGetProperty(n, out var v) ? (v.GetString() ?? "").Trim() : "";

    // 1) Gate behind the provisioning password (configured server-side; never hardcoded).
    var expected = config["ProvisioningSecret"] ?? "";
    if (string.IsNullOrWhiteSpace(expected) || S("provisionSecret") != expected)
    {
        auth.RecordLoginFailure(pKey);
        return Results.Json(new { success = false, error = "Invalid provisioning password." }, statusCode: 401);
    }
    auth.ClearLoginFailures(pKey);

    var gymName = S("gymName");
    var apiKey = S("apiKey");
    if (gymName.Length == 0 || apiKey.Length == 0)
        return Results.Json(new { success = false, error = "gymName and apiKey are required." }, statusCode: 400);

    using var master = await gymDb.GetMasterConnectionAsync();

    // 2) Idempotent: a gym with this API key already exists -> return it unchanged.
    using (var chk = new Npgsql.NpgsqlCommand(@"SELECT ""Subdomain"" FROM ""Gyms"" WHERE ""ApiKey"" = @k LIMIT 1", master))
    {
        chk.Parameters.AddWithValue("k", apiKey);
        var ex = await chk.ExecuteScalarAsync();
        if (ex != null)
            return Results.Json(new { success = true, subdomain = ex.ToString(), alreadyExisted = true });
    }

    // 3) Build a DNS-safe, unique subdomain from the requested one (or the gym name).
    string Slug(string s) => System.Text.RegularExpressions.Regex.Replace((s ?? "").ToLower(), "[^a-z0-9]", "");
    var baseSlug = Slug(S("subdomain").Length > 0 ? S("subdomain") : gymName);
    if (baseSlug.Length == 0) baseSlug = "gym";
    var reserved = new HashSet<string> { "www", "admin", "api" };
    string sub = baseSlug; int n = 1;
    while (true)
    {
        bool taken = reserved.Contains(sub);
        if (!taken)
        {
            using var u = new Npgsql.NpgsqlCommand(@"SELECT 1 FROM ""Gyms"" WHERE LOWER(""Subdomain"") = @s LIMIT 1", master);
            u.Parameters.AddWithValue("s", sub);
            taken = await u.ExecuteScalarAsync() != null;
        }
        if (!taken) break;
        sub = baseSlug + (++n);
    }

    var dbName = $"gymcloud_{sub}";

    // 4) Create the tenant DB + full schema (reuses the same path SuperAdmin uses).
    try { await gymDb.CreateGymDatabaseAsync(dbName); }
    catch (Npgsql.PostgresException pg) when (pg.SqlState == "42P04") { /* DB already exists - reuse it */ }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, error = "Could not create gym database: " + ex.Message }, statusCode: 500);
    }

    // 5) Write the master row LAST; if it fails, drop the just-created DB so a retry is clean.
    try
    {
        using var ins = new Npgsql.NpgsqlCommand(
            @"INSERT INTO ""Gyms"" (""Name"",""Subdomain"",""ApiKey"",""DatabaseName"",""OwnerName"",""OwnerPhone"",""OwnerEmail"",
              ""IsActive"",""ExpiresAt"",""CreatedAt"",""QrPoolEnabled"",""QrPoolSize"",""QrRangeStart"",""QrMonthlyFee"")
              VALUES (@n,@s,@k,@d,@on,@op,@oe,TRUE, NOW() + INTERVAL '1 year', NOW(), TRUE, 3000, 0, 0)", master);
        ins.Parameters.AddWithValue("n", gymName);
        ins.Parameters.AddWithValue("s", sub);
        ins.Parameters.AddWithValue("k", apiKey);
        ins.Parameters.AddWithValue("d", dbName);
        ins.Parameters.AddWithValue("on", S("ownerName"));
        ins.Parameters.AddWithValue("op", S("ownerPhone"));
        ins.Parameters.AddWithValue("oe", S("ownerEmail"));
        await ins.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        try
        {
            using var drop = new Npgsql.NpgsqlCommand($@"DROP DATABASE IF EXISTS ""{dbName}""", master);
            await drop.ExecuteNonQueryAsync();
        }
        catch { /* best-effort cleanup */ }
        return Results.Json(new { success = false, error = "Could not register gym: " + ex.Message }, statusCode: 500);
    }

    return Results.Json(new { success = true, subdomain = sub, alreadyExisted = false });
});

app.MapPost("/api/auth/login", async (HttpContext ctx, WebAuthService auth, SessionService sessions) =>
{
    // Read JSON body manually — minimal API model binding would require a DTO class
    using var doc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
    var root = doc.RootElement;
    var mode = root.TryGetProperty("mode", out var m) ? m.GetString() ?? "owner" : "owner";

    // If the request came from a gym subdomain (e.g. basmia.hmtech.solutions),
    // the GymSubdomainMiddleware has already resolved which gym DB to authenticate
    // against. Restricting login to that gym fixes the username-collision risk
    // and avoids the N-database scan in WebAuthService.FindUserDatabaseAsync.
    var requiredGym = AccessControlPro.Web.Middleware.GymContextAccessor.GetCurrentGym(ctx);

    AuthResult result;
    if (mode == "player")
    {
        var phone = root.TryGetProperty("phone", out var p) ? p.GetString() ?? "" : "";
        var cardLast4 = root.TryGetProperty("cardLast4", out var c) ? c.GetString() ?? "" : "";
        result = await auth.PlayerLoginAsync(phone, cardLast4, requiredGym);
    }
    else
    {
        var username = root.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
        var password = root.TryGetProperty("password", out var pw) ? pw.GetString() ?? "" : "";
        result = await auth.OwnerLoginAsync(username, password, requiredGym);
    }

    if (!result.IsAuthenticated)
        return Results.Ok(new { success = false, error = string.IsNullOrEmpty(result.Error) ? "Invalid credentials" : result.Error });

    var ip = ctx.Connection.RemoteIpAddress?.ToString();
    var ua = ctx.Request.Headers.UserAgent.ToString();
    // Players get a long-lived session so the installed PWA doesn't ask for credentials
    // on every launch; staff/owner keep the shorter default.
    var lifetime = result.Role == "Player" ? SessionService.PlayerLifetime : SessionService.DefaultLifetime;
    var token = await sessions.CreateAsync(
        result.Role, result.DisplayName, result.UserId,
        result.GymDatabase ?? "", result.GymId, ip, ua, lifetime);

    ctx.Response.Cookies.Append(SessionService.CookieName, token,
        BuildSessionCookie(ctx, lifetime));

    var redirect = result.Role == "Player" ? "/my" : "/dashboard";
    return Results.Ok(new { success = true, redirect });
});

app.MapPost("/api/auth/superadmin-login", async (HttpContext ctx, SessionService sessions, WebAuthService auth) =>
{
    // Brute-force protection (same 5-attempt / 15-min lockout as owner login).
    // Keyed by client IP since the SuperAdmin username is fixed.
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var throttleKey = $"superadmin:{ip}";
    if (auth.IsLoginBlocked(throttleKey))
        return Results.Ok(new { success = false, error = "Too many failed attempts. Please try again in 15 minutes." });

    using var doc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
    var root = doc.RootElement;
    var username = root.TryGetProperty("username", out var u) ? u.GetString() ?? "" : "";
    var password = root.TryGetProperty("password", out var p) ? p.GetString() ?? "" : "";

    var configUser = app.Configuration["SuperAdmin:Username"] ?? "";
    var configHash = app.Configuration["SuperAdmin:PasswordHash"] ?? "";

    bool hashOk = false;
    if (!string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(configHash))
    {
        if (configHash.StartsWith("$2"))
        {
            try { hashOk = BCrypt.Net.BCrypt.Verify(password, configHash); }
            catch { hashOk = false; }
        }
        else
        {
            var inputHash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(password)));
            hashOk = inputHash == configHash;
            if (hashOk)
                Console.WriteLine("[auth] WARN: SuperAdmin using legacy SHA256 hash. Migrate to BCrypt.");
        }
    }

    if (username.Trim() != configUser || !hashOk)
    {
        auth.RecordLoginFailure(throttleKey);
        return Results.Ok(new { success = false, error = "Invalid credentials" });
    }

    auth.ClearLoginFailures(throttleKey);
    var ua = ctx.Request.Headers.UserAgent.ToString();
    var token = await sessions.CreateAsync("SuperAdmin", "Super Admin", 0, "", 0, ip, ua);

    ctx.Response.Cookies.Append(SessionService.CookieName, token,
        BuildSessionCookie(ctx, SessionService.DefaultLifetime));

    return Results.Ok(new { success = true, redirect = "/superadmin/dashboard" });
});

app.MapPost("/api/auth/logout", async (HttpContext ctx, SessionService sessions) =>
{
    var token = ctx.Request.Cookies[SessionService.CookieName];
    if (!string.IsNullOrWhiteSpace(token))
        await sessions.RevokeAsync(token);

    ctx.Response.Cookies.Delete(SessionService.CookieName);
    return Results.Ok(new { success = true });
});

// SuperAdmin impersonation: swap the current SuperAdmin session for an Owner session
// scoped to the chosen gym. Used by the "View Gym" button on GymDetails. Requires a
// valid SuperAdmin cookie on the caller — no other role can trigger this.
app.MapPost("/api/auth/impersonate", async (HttpContext ctx, GymDbHelper gymDb, SessionService sessions) =>
{
    // Only SuperAdmin can impersonate. Validate the current caller session first.
    var callerToken = ctx.Request.Cookies[SessionService.CookieName];
    if (string.IsNullOrWhiteSpace(callerToken))
        return Results.Unauthorized();
    var caller = await sessions.ValidateAsync(callerToken, null, null);
    if (caller == null || caller.Role != "SuperAdmin")
        return Results.Unauthorized();

    using var doc = await System.Text.Json.JsonDocument.ParseAsync(ctx.Request.Body);
    var root = doc.RootElement;
    var gymDbName = root.TryGetProperty("gymDbName", out var g) ? g.GetString() ?? "" : "";
    var gymId = root.TryGetProperty("gymId", out var i) && i.TryGetInt32(out var gid) ? gid : 0;
    if (string.IsNullOrWhiteSpace(gymDbName) || gymId == 0)
        return Results.BadRequest(new { error = "Missing gymDbName or gymId" });

    // Find an active admin user in that gym's DB to impersonate.
    using var gymConn = await gymDb.GetGymConnectionAsync(gymDbName);
    using var lookup = new Npgsql.NpgsqlCommand(
        @"SELECT ""Id"", ""DisplayName"", ""Role"" FROM ""Users""
          WHERE ""Role"" IN ('Admin','Owner') AND ""IsActive"" = TRUE
          ORDER BY ""Id"" LIMIT 1", gymConn);
    using var reader = await lookup.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.BadRequest(new { error = "No active admin user found in this gym" });

    var userId = reader.GetInt32(0);
    var displayName = reader.GetString(1) + " (Support View)";
    var role = reader.GetString(2);

    // Revoke the SuperAdmin session and issue a new one for the impersonated role.
    // Forces SuperAdmin to re-login via /superadmin/login to regain their powers,
    // so impersonation can't be quietly abused during a long session.
    await sessions.RevokeAsync(callerToken);

    var ip = ctx.Connection.RemoteIpAddress?.ToString();
    var ua = ctx.Request.Headers.UserAgent.ToString();
    var newToken = await sessions.CreateAsync(role, displayName, userId, gymDbName, gymId, ip, ua);

    ctx.Response.Cookies.Append(SessionService.CookieName, newToken,
        BuildSessionCookie(ctx, SessionService.DefaultLifetime));

    Console.WriteLine($"[impersonate] SuperAdmin session revoked; impersonating {role} in gym {gymId} ({gymDbName})");
    return Results.Ok(new { success = true, redirect = "/dashboard" });
});

// ============================================================================
// Diagnostics — WPF main app uploads a ZIP of logs + system snapshot here so
// support can investigate issues without an AnyDesk session. Auth reuses the
// same X-Api-Key that the sync client already sends; the key resolves to a
// specific gym row so bundles are stored per-tenant automatically.
// ============================================================================
app.MapPost("/api/diagnostics/upload", async (HttpContext context, GymDbHelper gymDb) =>
{
    if (!await IsApiKeyValidAsync(context, gymDb))
        return Results.Unauthorized();

    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault() ?? "";
    var trigger = context.Request.Headers["X-Trigger"].FirstOrDefault() ?? "manual";
    var appVersion = context.Request.Headers["X-App-Version"].FirstOrDefault() ?? "unknown";

    // Resolve which gym this bundle belongs to so we can scope storage + the row.
    int gymId = 0;
    string gymName = "";
    try
    {
        using var master = await gymDb.GetMasterConnectionAsync();
        using var cmd = new Npgsql.NpgsqlCommand(
            @"SELECT ""Id"", ""Name"" FROM ""Gyms"" WHERE ""ApiKey"" = @k AND ""IsActive"" = TRUE", master);
        cmd.Parameters.AddWithValue("k", apiKey);
        using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            gymId = r.GetInt32(0);
            gymName = r.GetString(1);
        }
    }
    catch (Exception ex) { Console.WriteLine($"[diagnostics] gym resolve failed: {ex.Message}"); }

    // 25 MB hard cap on the wire (WPF caps the bundle at 20 MB; this gives headroom)
    const long MaxBundleBytes = 25L * 1024 * 1024;

    // Stream the body to a temp file first so we don't hold large requests in memory
    var storageRoot = app.Configuration["DiagnosticsStoragePath"];
    if (string.IsNullOrWhiteSpace(storageRoot))
        storageRoot = Path.Combine(app.Environment.ContentRootPath, "diagnostics");

    var gymFolder = Path.Combine(storageRoot, gymId > 0 ? gymId.ToString() : "_unknown");
    Directory.CreateDirectory(gymFolder);

    var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    var fileName = $"{stamp}.zip";
    var fullPath = Path.Combine(gymFolder, fileName);

    long bytesWritten = 0;
    try
    {
        using (var fs = File.Create(fullPath))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await context.Request.Body.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                bytesWritten += read;
                if (bytesWritten > MaxBundleBytes)
                {
                    fs.Close();
                    try { File.Delete(fullPath); } catch { }
                    return Results.BadRequest($"Bundle exceeds {MaxBundleBytes / 1024 / 1024} MB limit");
                }
                await fs.WriteAsync(buffer.AsMemory(0, read));
            }
        }

        // Insert metadata row in master DB
        int newId;
        using (var master = await gymDb.GetMasterConnectionAsync())
        using (var ins = new Npgsql.NpgsqlCommand(
            @"INSERT INTO ""DiagnosticsUploads""
                (""GymId"", ""FileName"", ""FilePath"", ""FileSizeBytes"", ""AppVersion"", ""Trigger"", ""UploadedAt"")
              VALUES (@gym, @fn, @path, @size, @ver, @trig, @ts)
              RETURNING ""Id""", master))
        {
            ins.Parameters.AddWithValue("gym", gymId);
            ins.Parameters.AddWithValue("fn", fileName);
            ins.Parameters.AddWithValue("path", fullPath);
            ins.Parameters.AddWithValue("size", bytesWritten);
            ins.Parameters.AddWithValue("ver", appVersion);
            ins.Parameters.AddWithValue("trig", trigger);
            ins.Parameters.AddWithValue("ts", DateTime.UtcNow);
            newId = Convert.ToInt32(await ins.ExecuteScalarAsync());
        }

        // Cleanup: keep only the 12 most-recent bundles per gym
        if (gymId > 0)
        {
            try
            {
                using var master = await gymDb.GetMasterConnectionAsync();
                using var sel = new Npgsql.NpgsqlCommand(
                    @"SELECT ""Id"", ""FilePath"" FROM ""DiagnosticsUploads""
                      WHERE ""GymId"" = @g
                      ORDER BY ""UploadedAt"" DESC
                      OFFSET 12", master);
                sel.Parameters.AddWithValue("g", gymId);
                var toDelete = new List<(int id, string path)>();
                using (var rr = await sel.ExecuteReaderAsync())
                    while (await rr.ReadAsync())
                        toDelete.Add((rr.GetInt32(0), rr.IsDBNull(1) ? "" : rr.GetString(1)));

                foreach (var (id, path) in toDelete)
                {
                    try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch { }
                    using var del = new Npgsql.NpgsqlCommand(@"DELETE FROM ""DiagnosticsUploads"" WHERE ""Id"" = @i", master);
                    del.Parameters.AddWithValue("i", id);
                    await del.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex) { Console.WriteLine($"[diagnostics] cleanup failed: {ex.Message}"); }
        }

        Console.WriteLine($"[diagnostics] uploaded {bytesWritten / 1024} KB from gym {gymId} ({gymName}), trigger={trigger}, v={appVersion}");
        return Results.Ok(new { success = true, id = newId, sizeBytes = bytesWritten });
    }
    catch (Exception ex)
    {
        try { if (File.Exists(fullPath)) File.Delete(fullPath); } catch { }
        Console.WriteLine($"[diagnostics] upload exception: {ex}");
        return Results.Problem("Diagnostics upload failed");
    }
});

// SuperAdmin-only download — streams a stored bundle back to the browser.
// Auth: the SessionInfo cookie populated by the early middleware must carry Role=SuperAdmin.
app.MapGet("/api/diagnostics/download/{id:int}", async (int id, HttpContext context, GymDbHelper gymDb) =>
{
    var session = context.Items["SessionInfo"] as AccessControlPro.Web.Services.SessionInfo;
    if (session == null || session.Role != "SuperAdmin")
        return Results.Unauthorized();

    string? filePath = null;
    string? fileName = null;
    using (var master = await gymDb.GetMasterConnectionAsync())
    using (var cmd = new Npgsql.NpgsqlCommand(
        @"SELECT ""FilePath"", ""FileName"" FROM ""DiagnosticsUploads"" WHERE ""Id"" = @i", master))
    {
        cmd.Parameters.AddWithValue("i", id);
        using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            filePath = r.IsDBNull(0) ? null : r.GetString(0);
            fileName = r.IsDBNull(1) ? null : r.GetString(1);
        }
    }

    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        return Results.NotFound("Bundle not found on disk");

    var stream = File.OpenRead(filePath);
    return Results.File(stream, "application/zip", fileName ?? $"diagnostics-{id}.zip");
});

// ============================================================================
// Version manifest — WPF app polls this on launch to see if a new build is
// available. Public (no API key), so installs that haven't completed setup yet
// can still check for updates. Manifest is a static JSON file on disk; pushing
// a new release = scp the .zip into wwwroot/releases/ and edit latest.json.
// See tools/release-management/README.md for the deploy recipe.
// ============================================================================
app.MapGet("/api/version/latest", async (HttpContext context) =>
{
    var manifestPath = app.Configuration["VersionManifestPath"];
    if (string.IsNullOrWhiteSpace(manifestPath))
        manifestPath = Path.Combine(app.Environment.WebRootPath, "releases", "latest.json");

    if (!File.Exists(manifestPath))
        return Results.NotFound(new { error = "No version manifest published yet" });

    try
    {
        var json = await File.ReadAllTextAsync(manifestPath);
        // Don't cache aggressively — we want customers to pick up new releases the
        // same day we publish, not on next CDN-edge-expiry.
        context.Response.Headers["Cache-Control"] = "public, max-age=300"; // 5 min
        return Results.Content(json, "application/json");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[api/version] read failed: {ex.Message}");
        return Results.Problem("Failed to read version manifest");
    }
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
