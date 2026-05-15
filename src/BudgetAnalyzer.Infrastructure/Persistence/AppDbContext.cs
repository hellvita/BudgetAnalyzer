using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetAnalyzer.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly IClock _clock;

    public AppDbContext(DbContextOptions<AppDbContext> options, IClock clock) : base(options)
    {
        _clock = clock;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<DailyExpense> DailyExpenses => Set<DailyExpense>();
    public DbSet<DailyIncome> DailyIncomes => Set<DailyIncome>();
    public DbSet<DailyLimit> DailyLimits => Set<DailyLimit>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                switch (entry.Entity)
                {
                    case User u: u.CreatedAt = now; u.UpdatedAt = now; break;
                    case Category c: c.CreatedAt = now; c.UpdatedAt = now; break;
                    case DailyExpense e: e.CreatedAt = now; e.UpdatedAt = now; break;
                    case DailyIncome i: i.CreatedAt = now; i.UpdatedAt = now; break;
                    case DailyLimit l: l.CreatedAt = now; break;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                switch (entry.Entity)
                {
                    case User u: u.UpdatedAt = now; break;
                    case Category c: c.UpdatedAt = now; break;
                    case DailyExpense e: e.UpdatedAt = now; break;
                    case DailyIncome i: i.UpdatedAt = now; break;
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
