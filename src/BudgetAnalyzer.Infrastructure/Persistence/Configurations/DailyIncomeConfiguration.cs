using BudgetAnalyzer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetAnalyzer.Infrastructure.Persistence.Configurations;

public class DailyIncomeConfiguration : IEntityTypeConfiguration<DailyIncome>
{
    public void Configure(EntityTypeBuilder<DailyIncome> builder)
    {
        builder.ToTable("daily_incomes");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.UserId).HasColumnName("user_id");
        builder.Property(i => i.Date).HasColumnName("date");
        builder.Property(i => i.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");

        // One income entry per user per day
        builder.HasIndex(i => new { i.UserId, i.Date })
            .IsUnique()
            .HasDatabaseName("ix_daily_incomes_userid_date");
    }
}
