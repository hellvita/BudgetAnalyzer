using BudgetAnalyzer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetAnalyzer.Infrastructure.Persistence.Configurations;

public class DailyLimitConfiguration : IEntityTypeConfiguration<DailyLimit>
{
    public void Configure(EntityTypeBuilder<DailyLimit> builder)
    {
        builder.ToTable("daily_limits");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.UserId).HasColumnName("user_id");
        builder.Property(l => l.EffectiveFromDate).HasColumnName("effective_from_date");
        builder.Property(l => l.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");

        // One limit entry per (user, effective_from_date) — updating reuses the same row
        builder.HasIndex(l => new { l.UserId, l.EffectiveFromDate })
            .IsUnique()
            .HasDatabaseName("ix_daily_limits_userid_effectivefrom");

        // Descending index supports the "latest limit on or before date D" query efficiently
        builder.HasIndex(l => new { l.UserId, l.EffectiveFromDate })
            .IsDescending(false, true)
            .HasDatabaseName("ix_daily_limits_userid_effectivefrom_desc");
    }
}
