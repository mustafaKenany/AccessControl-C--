using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.CompanyName).HasMaxLength(200);
        builder.Property(a => a.GymName).HasMaxLength(200);
        builder.Property(a => a.LogoPath).HasMaxLength(500);
        builder.Property(a => a.DevLogoPath).HasMaxLength(500);
        builder.Property(a => a.Phone).HasMaxLength(100);
        builder.Property(a => a.Address).HasMaxLength(500);
    }
}
