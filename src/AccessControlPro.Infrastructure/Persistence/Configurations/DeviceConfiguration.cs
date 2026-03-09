using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.IP).HasMaxLength(50).IsRequired();
        builder.Property(d => d.MAC).HasMaxLength(50);
        builder.Property(d => d.SerialNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(d => d.SerialNumber).IsUnique();
        builder.Property(d => d.Password).HasMaxLength(20);
        builder.Property(d => d.Gateway).HasMaxLength(50);
        builder.Property(d => d.SubnetMask).HasMaxLength(50);
        builder.HasMany(d => d.Doors).WithOne(door => door.Device).HasForeignKey(door => door.DeviceId);
    }
}
