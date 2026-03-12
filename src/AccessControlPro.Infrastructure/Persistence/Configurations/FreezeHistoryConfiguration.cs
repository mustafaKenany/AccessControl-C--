using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class FreezeHistoryConfiguration : IEntityTypeConfiguration<FreezeHistory>
{
    public void Configure(EntityTypeBuilder<FreezeHistory> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Reason).HasMaxLength(500);

        builder.HasIndex(e => e.EmployeeId);

        builder.HasOne(e => e.Employee)
            .WithMany(e => e.FreezeHistories)
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
