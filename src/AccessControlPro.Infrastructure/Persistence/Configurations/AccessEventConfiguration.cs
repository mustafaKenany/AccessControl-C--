using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class AccessEventConfiguration : IEntityTypeConfiguration<AccessEvent>
{
    public void Configure(EntityTypeBuilder<AccessEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Details).HasMaxLength(500);
        builder.HasIndex(e => e.Timestamp);
        builder.HasOne(e => e.Door).WithMany(d => d.AccessEvents).HasForeignKey(e => e.DoorId);
        builder.HasOne(e => e.Card).WithMany(c => c.AccessEvents).HasForeignKey(e => e.CardId).IsRequired(false);
    }
}
