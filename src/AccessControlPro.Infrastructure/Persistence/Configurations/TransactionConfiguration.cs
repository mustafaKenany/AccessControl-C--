using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Category).HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.CreatedBy).HasMaxLength(100);
        builder.Property(t => t.Amount).HasPrecision(18, 2);

        builder.HasIndex(t => t.CreatedAt);
        builder.HasIndex(t => t.Type);
        builder.HasIndex(t => t.RelatedEmployeeId);

        builder.HasOne(t => t.RelatedEmployee)
            .WithMany()
            .HasForeignKey(t => t.RelatedEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
