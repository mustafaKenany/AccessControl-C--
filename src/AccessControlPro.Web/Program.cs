using AccessControlPro.Web.Components;
using AccessControlPro.Web.Data;
using AccessControlPro.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// PostgreSQL CloudDbContext
var cloudConn = builder.Configuration.GetConnectionString("CloudConnection")
    ?? "Host=localhost;Database=gymcloud;Username=postgres;Password=postgres";
builder.Services.AddDbContext<CloudDbContext>(options =>
    options.UseNpgsql(cloudConn));

// Auth + session services
builder.Services.AddSingleton<WebAuthService>();
builder.Services.AddScoped<SessionState>();

var app = builder.Build();

// Auto-create database tables on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<CloudDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database init warning: {ex.Message}");
    }
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
