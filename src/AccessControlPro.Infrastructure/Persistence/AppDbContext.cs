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
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<LookupItem> LookupItems => Set<LookupItem>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<CardDeviceSync> CardDeviceSyncs => Set<CardDeviceSync>();
    public DbSet<QrPass> QrPasses => Set<QrPass>();
    public DbSet<MonitorLock> MonitorLocks => Set<MonitorLock>();
    public DbSet<TimeGroup> TimeGroups => Set<TimeGroup>();
    public DbSet<PosShift> PosShifts => Set<PosShift>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // v4.5 added AFTER UPDATE triggers on these tables to keep UpdatedAt fresh for
        // delta cloud sync. Without telling EF Core about them, SaveChanges() on these
        // entities throws "Could not save changes because the target table has database
        // triggers" because EF's default INSERT...OUTPUT pattern is incompatible with
        // triggers. Registering each trigger here switches EF to a save strategy that
        // works alongside them. See aka.ms/efcore-docs-sqlserver-save-changes-and-output-clause.
        modelBuilder.Entity<Employee>().ToTable(t => t.HasTrigger("TR_Employees_UpdatedAt"));
        modelBuilder.Entity<AccessCard>().ToTable(t => t.HasTrigger("TR_AccessCards_UpdatedAt"));
        modelBuilder.Entity<AppUser>().ToTable(t => t.HasTrigger("TR_Users_UpdatedAt"));
        modelBuilder.Entity<FreezeHistory>().ToTable(t => t.HasTrigger("TR_FreezeHistories_UpdatedAt"));
        modelBuilder.Entity<Product>().ToTable(t => t.HasTrigger("TR_Products_UpdatedAt"));
        modelBuilder.Entity<PosShift>().ToTable(t => t.HasTrigger("TR_PosShifts_UpdatedAt"));
    }
}
