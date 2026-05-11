using BudgetAnalyzer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetAnalyzer.Infrastructure.Persistence.Configurations;

public class DailyExpenseConfiguration : IEntityTypeConfiguration<DailyExpense>
{
    public void Configure(EntityTypeBuilder<DailyExpense> builder)
    {
        builder.ToTable("daily_expenses");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.UserId).HasColumnName("user_id");
        builder.Property(e => e.CategoryId).HasColumnName("category_id");
        builder.Property(e => e.Date).HasColumnName("date");
        builder.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // One row per (user, category, date) — enforced at DB level
        builder.HasIndex(e => new { e.UserId, e.CategoryId, e.Date })
            .IsUnique()
            .HasDatabaseName("ix_daily_expenses_userid_categoryid_date");

        // Restrict delete so removing a category doesn't silently wipe its expense history
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
