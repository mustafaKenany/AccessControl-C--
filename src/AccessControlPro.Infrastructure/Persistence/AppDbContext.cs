using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccessControlPro.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Door> Doors => Set<Door>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<AccessCard> AccessCards => Set<AccessCard>();
    public DbSet<AccessEvent> AccessEvents => Set<AccessEvent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DeletedEmployee> DeletedEmployees => Set<DeletedEmployee>();
    public DbSet<FreezeHistory> FreezeHistories => Set<FreezeHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
