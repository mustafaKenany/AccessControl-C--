using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.UnitPrice).HasPrecision(18, 2);
        builder.Property(m => m.Reference).HasMaxLength(200);
        builder.Property(m => m.Description).HasMaxLength(500);
        builder.Property(m => m.CreatedBy).HasMaxLength(100);

        builder.HasOne(m => m.Product)
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.PurchaseOrder)
            .WithMany()
            .HasForeignKey(m => m.PurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.ProductId);
        builder.HasIndex(m => m.CreatedAt);
        builder.HasIndex(m => m.Type);
    }
}
