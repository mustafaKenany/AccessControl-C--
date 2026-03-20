using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class CardDeviceSyncConfiguration : IEntityTypeConfiguration<CardDeviceSync>
{
    public void Configure(EntityTypeBuilder<CardDeviceSync> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.LastError).HasMaxLength(500);

        builder.HasOne(c => c.AccessCard)
            .WithMany(a => a.DeviceSyncs)
            .HasForeignKey(c => c.AccessCardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Device)
            .WithMany(d => d.CardSyncs)
            .HasForeignKey(c => c.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.AccessCardId, c.DeviceId }).IsUnique();
        builder.HasIndex(c => c.DeviceId);
        builder.HasIndex(c => c.AccessCardId);
    }
}
