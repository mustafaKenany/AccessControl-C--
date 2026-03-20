using Microsoft.EntityFrameworkCore;
using AccessControlPro.Domain.Entities;

namespace AccessControlPro.Web.Data;

public class CloudDbContext : DbContext
{
    public CloudDbContext(DbContextOptions<CloudDbContext> options) : base(options) { }

    public DbSet<Employee> Players { get; set; }
    public DbSet<AccessCard> AccessCards { get; set; }
    public DbSet<AccessEvent> AccessEvents { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<Door> Doors { get; set; }
    public DbSet<AppSettings> Settings { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<AppUser> Users { get; set; }

    // Cloud-only tables
    public DbSet<CloudSyncLog> SyncLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Map to same table names as SQL Server
        modelBuilder.Entity<Employee>(e =>
        {
            e.ToTable("Players");
            e.Ignore(x => x.AccessCards);
            e.Ignore(x => x.FreezeHistories);
            e.Ignore(x => x.RowVersion);
            e.Ignore(x => x.PhotoData);
        });

        modelBuilder.Entity<AccessCard>(e =>
        {
            e.ToTable("AccessCards");
            e.Ignore(x => x.Employee);
            e.Ignore(x => x.AccessEvents);
            e.Ignore(x => x.DeviceSyncs);
        });

        modelBuilder.Entity<AccessEvent>(e =>
        {
            e.ToTable("AccessEvents");
            e.Ignore(x => x.Door);
            e.Ignore(x => x.Card);
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("Devices");
            e.Ignore(x => x.Doors);
            e.Ignore(x => x.CardSyncs);
        });

        modelBuilder.Entity<Door>(e =>
        {
            e.ToTable("Doors");
            e.Ignore(x => x.Device);
            e.Ignore(x => x.AccessEvents);
        });

        modelBuilder.Entity<AppSettings>().ToTable("AppSettings");

        modelBuilder.Entity<Transaction>(e =>
        {
            e.ToTable("Transactions");
            e.Ignore(x => x.RelatedEmployee);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("Users");
        });

        modelBuilder.Entity<CloudSyncLog>().ToTable("CloudSyncLogs");
    }
}
