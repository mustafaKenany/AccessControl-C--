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
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=604800"); // 7 days
    }
});
app.UseAntiforgery();

// Sync API — receives data from WPF app via HTTPS
app.MapPost("/api/sync", async (HttpContext context, DbHelper db) =>
{
    // Verify API key
    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
    if (apiKey != app.Configuration["SyncApiKey"] && apiKey != "HMTech-Sync-2026")
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

        using var conn = await db.GetConnectionAsync();
        int total = 0;

        // Process each table
        if (syncData.Players?.Count > 0)
            total += await SyncHelper.UpsertRowsAsync(conn, "Players", syncData.Players);
        if (syncData.AccessEvents?.Count > 0)
            total += await SyncHelper.UpsertRowsAsync(conn, "AccessEvents", syncData.AccessEvents);
        if (syncData.Devices?.Count > 0)
            total += await SyncHelper.UpsertRowsAsync(conn, "Devices", syncData.Devices);
        if (syncData.Doors?.Count > 0)
            total += await SyncHelper.UpsertRowsAsync(conn, "Doors", syncData.Doors);
        if (syncData.Transactions?.Count > 0)
            total += await SyncHelper.UpsertRowsAsync(conn, "Transactions", syncData.Transactions);
        if (syncData.Users?.Count > 0)
            total += await SyncHelper.UpsertRowsAsync(conn, "Users", syncData.Users);
        if (syncData.AuditLogs?.Count > 0)
            total += await SyncHelper.UpsertRowsAsync(conn, "AuditLogs", syncData.AuditLogs);
        if (syncData.DeletedEmployees?.Count > 0)
            total += await SyncHelper.UpsertRowsAsync(conn, "DeletedEmployees", syncData.DeletedEmployees);
        if (syncData.AppSettings?.Count > 0)
            total += await SyncHelper.UpsertRowsAsync(conn, "AppSettings", syncData.AppSettings);
        if (syncData.AccessCards?.Count > 0)
            total += await SyncHelper.UpsertRowsAsync(conn, "AccessCards", syncData.AccessCards);

        // Invalidate cached data after sync
        QueryCache.InvalidateAll();

        // Get errors from SyncHelper
        var syncErrors = SyncHelper.GetLastErrors();
        var status = syncErrors.Count == 0 ? "Success" : "PartialSuccess";
        var details = $"Synced {total} records via API";
        if (syncErrors.Count > 0)
            details += " | Errors: " + string.Join("; ", syncErrors.Take(10));

        // Log sync
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
        catch { }

        return Results.Ok(new { success = true, total, errors = syncErrors });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Sync failed: {ex.Message}");
    }
});

// Pull users API — local app pulls new users from cloud
app.MapGet("/api/users", async (HttpContext context, DbHelper db) =>
{
    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
    if (apiKey != app.Configuration["SyncApiKey"] && apiKey != "HMTech-Sync-2026")
        return Results.Unauthorized();

    try
    {
        using var conn = await db.GetConnectionAsync();
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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
