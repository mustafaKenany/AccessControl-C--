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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
