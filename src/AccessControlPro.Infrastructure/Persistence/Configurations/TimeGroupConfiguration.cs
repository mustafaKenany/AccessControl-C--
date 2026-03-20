using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class TimeGroupConfiguration : IEntityTypeConfiguration<TimeGroup>
{
    public void Configure(EntityTypeBuilder<TimeGroup> builder)
    {
        builder.ToTable("TimeGroups");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(t => t.NameAr).HasMaxLength(100);
        builder.Property(t => t.ScheduleJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(t => t.HardwareIndex).IsUnique();
    }
}
