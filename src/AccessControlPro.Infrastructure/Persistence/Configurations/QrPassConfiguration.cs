using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class QrPassConfiguration : IEntityTypeConfiguration<QrPass>
{
    public void Configure(EntityTypeBuilder<QrPass> builder)
    {
        builder.HasKey(q => q.Id);

        builder.Property(q => q.PassCode).HasMaxLength(100);
        builder.Property(q => q.PlayerName).HasMaxLength(200);
        builder.Property(q => q.Phone).HasMaxLength(100);
        builder.Property(q => q.Fee).HasPrecision(18, 2);
        builder.Property(q => q.CreatedBy).HasMaxLength(100);
        builder.Property(q => q.DeviceName).HasMaxLength(200);

        builder.HasIndex(q => q.PassCode).IsUnique();
        builder.HasIndex(q => new { q.IsActive, q.ValidTo });
    }
}
