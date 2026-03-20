using AccessControlPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlPro.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FullNameEn).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FullNameAr).HasMaxLength(200);
        builder.Property(e => e.CardNo).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.CardNo).IsUnique();
        builder.Property(e => e.SubscriptionType).HasMaxLength(100);
        builder.Property(e => e.Phone).HasMaxLength(30);
        builder.HasIndex(e => e.Phone).IsUnique();
        builder.Property(e => e.PhotoData).HasColumnType("varbinary(max)");
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.SubscriptionFee).HasPrecision(18, 2);
        builder.Property(e => e.AmountPaid).HasPrecision(18, 2);
        builder.Property(e => e.CardBalance).HasPrecision(18, 2);

        // Optimistic concurrency — SQL Server auto-increments rowversion on each UPDATE
        builder.Property(e => e.RowVersion).IsRowVersion();

        // Indexes for fast search
        builder.HasIndex(e => e.FullNameEn);
        builder.HasIndex(e => e.FullNameAr);
        builder.HasIndex(e => e.SubscriptionType);
        builder.HasIndex(e => e.EndDate);

        builder.HasMany(e => e.AccessCards).WithOne(c => c.Employee).HasForeignKey(c => c.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.FreezeHistories).WithOne(e => e.Employee).HasForeignKey(e => e.EmployeeId);
    }
}
