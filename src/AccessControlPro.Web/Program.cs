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

var cloudConn = builder.Configuration.GetConnectionString("CloudConnection")
    ?? "Host=localhost;Database=gymcloud;Username=postgres;Password=GymCloud2026";

builder.Services.AddSingleton(new DbHelper(cloudConn));
builder.Services.AddSingleton(new GymDbHelper(cloudConn));
builder.Services.AddSingleton<WebAuthService>();
builder.Services.AddScoped<SessionState>();

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

var app = builder.Build();

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
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=300"); // 5 min — prevents stale CSS
    }
});
app.UseAntiforgery();

// Sync API — receives data from WPF app via HTTPS
app.MapPost("/api/sync", async (HttpContext context, DbHelper db, GymDbHelper gymDb) =>
{
    // Verify API key — accept config key, default key, OR any valid gym key
    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
    var isValidKey = apiKey == app.Configuration["SyncApiKey"];
    if (!isValidKey && !string.IsNullOrEmpty(apiKey))
    {
        var gymDb2 = await gymDb.GetDatabaseByApiKeyAsync(apiKey);
        isValidKey = !string.IsNullOrEmpty(gymDb2);
    }
    if (!isValidKey)
    {
        return Results.Unauthorized();
    }

    try
    {
        // Support gzip-compressed request bodies
        string json;
        if (context.Request.Headers.ContentEncoding.ToString().Contains("gzip"))
        {
            using var decompressed = new System.IO.MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(context.Request.Body, System.IO.Compression.CompressionMode.Decompress))
            {
                await gzip.CopyToAsync(decompressed);
            }
            json = System.Text.Encoding.UTF8.GetString(decompressed.ToArray());
        }
        else
        {
            using var reader = new StreamReader(context.Request.Body);
            json = await reader.ReadToEndAsync();
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
                  WHERE ""ApiKey"" = @key OR ""Id"" = 1", masterConn);
            updateCmd.Parameters.AddWithValue("ts", DateTime.UtcNow);
            updateCmd.Parameters.AddWithValue("pc", playerCount);
            updateCmd.Parameters.AddWithValue("key", apiKey ?? "");
            await updateCmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex) { Console.WriteLine($"[Program] UpdateGymSync Error: {ex.Message}"); }

        return Results.Ok(new { success = true, total, errors = syncErrors });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Sync failed: {ex.Message}");
    }
});

// Pull users API — local app pulls new users from cloud
app.MapGet("/api/users", async (HttpContext context, GymDbHelper gymDb, DbHelper db) =>
{
    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
    var isValid = apiKey == app.Configuration["SyncApiKey"];
    if (!isValid && !string.IsNullOrEmpty(apiKey))
        isValid = !string.IsNullOrEmpty(await gymDb.GetDatabaseByApiKeyAsync(apiKey));
    if (!isValid)
        return Results.Unauthorized();

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
        return Results.Problem($"Failed to get users: {ex.Message}");
    }
});

// QR Pool API — local app pulls cloud-assigned QR codes
app.MapGet("/api/qr-pool", async (HttpContext context, GymDbHelper gymDb) =>
{
    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
    var isValid = apiKey == app.Configuration["SyncApiKey"];
    if (!isValid && !string.IsNullOrEmpty(apiKey))
        isValid = !string.IsNullOrEmpty(await gymDb.GetDatabaseByApiKeyAsync(apiKey));
    if (!isValid)
        return Results.Unauthorized();

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
        return Results.Problem($"Failed to get QR pool: {ex.Message}");
    }
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
