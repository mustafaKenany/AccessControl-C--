using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class DeletedEmployeeConfiguration : IEntityTypeConfiguration<DeletedEmployee>
{
    public void Configure(EntityTypeBuilder<DeletedEmployee> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FullNameEn).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FullNameAr).HasMaxLength(200).IsRequired();
        builder.Property(e => e.CardNo).HasMaxLength(50).IsRequired();
        builder.Property(e => e.SubscriptionType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(30).IsRequired();
        builder.Property(e => e.PhotoData).HasColumnType("varbinary(max)");
        builder.Property(e => e.Notes).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.DeleteReason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.DeletedBy).HasMaxLength(100).IsRequired();
        builder.Property(e => e.SubscriptionFee).HasPrecision(18, 2);
        builder.Property(e => e.AmountPaid).HasPrecision(18, 2);

        builder.HasIndex(e => e.OriginalId);
        builder.HasIndex(e => e.DeletedAt);
    }
}
