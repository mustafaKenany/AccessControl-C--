using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class LookupItemConfiguration : IEntityTypeConfiguration<LookupItem>
{
    public void Configure(EntityTypeBuilder<LookupItem> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Category).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.NameAr).HasMaxLength(200);
        builder.Property(l => l.NumericValue).HasPrecision(18, 2);

        builder.HasIndex(l => new { l.Category, l.IsActive, l.SortOrder });
    }
}
