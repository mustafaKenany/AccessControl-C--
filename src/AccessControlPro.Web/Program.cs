using AccessControlPro.Web.Components;
using AccessControlPro.Web.Data;
using AccessControlPro.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var cloudConn = builder.Configuration.GetConnectionString("CloudConnection")
    ?? "Host=localhost;Database=gymcloud;Username=postgres;Password=GymCloud2026";

builder.Services.AddSingleton(new DbHelper(cloudConn));
builder.Services.AddSingleton<WebAuthService>();
builder.Services.AddScoped<SessionState>();

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

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
