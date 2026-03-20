using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.NameAr).HasMaxLength(200);
        builder.Property(p => p.Category).HasMaxLength(100);
        builder.Property(p => p.Barcode).HasMaxLength(100);
        builder.Property(p => p.Price).HasPrecision(18, 2);

        builder.HasIndex(p => p.IsActive);
        builder.HasIndex(p => p.Category);
    }
}
