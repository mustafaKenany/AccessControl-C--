using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class AccessCardConfiguration : IEntityTypeConfiguration<AccessCard>
{
    public void Configure(EntityTypeBuilder<AccessCard> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.CardNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(c => c.CardNumber).IsUnique();
        builder.Property(c => c.CardPassword).HasMaxLength(10);
        builder.Property(c => c.CardType).HasMaxLength(30);
        builder.Property(c => c.DoorPermissions).HasMaxLength(20);

        builder.HasMany(c => c.AccessEvents).WithOne(e => e.Card).HasForeignKey(e => e.CardId);
    }
}
