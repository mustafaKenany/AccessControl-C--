using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.HasKey(po => po.Id);

        builder.Property(po => po.TotalAmount).HasPrecision(18, 2);
        builder.Property(po => po.Discount).HasPrecision(18, 2);
        builder.Property(po => po.AmountPaid).HasPrecision(18, 2);
        builder.Property(po => po.PaymentStatus).HasMaxLength(50);
        builder.Property(po => po.Notes).HasMaxLength(1000);
        builder.Property(po => po.CreatedBy).HasMaxLength(100);

        builder.HasOne(po => po.Supplier)
            .WithMany()
            .HasForeignKey(po => po.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(po => po.SupplierId);
        builder.HasIndex(po => po.OrderDate);
    }
}
