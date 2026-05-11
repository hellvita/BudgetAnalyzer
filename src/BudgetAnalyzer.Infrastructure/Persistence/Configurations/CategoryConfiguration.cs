using BudgetAnalyzer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetAnalyzer.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(128).IsRequired();
        builder.Property(c => c.IsArchived).HasColumnName("is_archived");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        // Partial unique: names must be unique per user among active (non-archived) categories only.
        // Archived categories may share a name so the user can re-create them later.
        builder.HasIndex(c => new { c.UserId, c.Name })
            .IsUnique()
            .HasFilter("is_archived = false")
            .HasDatabaseName("ix_categories_userid_name_active");
    }
}
