using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Phone).HasMaxLength(100);
        builder.Property(s => s.Address).HasMaxLength(500);
        builder.Property(s => s.ContactPerson).HasMaxLength(200);

        builder.HasIndex(s => s.IsActive).HasFilter("IsActive = 1");
        builder.HasIndex(s => s.Name);
    }
}
