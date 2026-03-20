using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AccessControlPro.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        // Design-time only — used by EF migrations tooling, never in production
        var connString = Environment.GetEnvironmentVariable("ACP_DESIGN_CONNECTION")
            ?? "Server=localhost;Database=AccessControlPro;Integrated Security=True;TrustServerCertificate=True;";
        optionsBuilder.UseSqlServer(connString);
        return new AppDbContext(optionsBuilder.Options);
    }
}
