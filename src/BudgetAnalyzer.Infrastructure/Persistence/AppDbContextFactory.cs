using BudgetAnalyzer.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace BudgetAnalyzer.Infrastructure.Persistence;

// Used by `dotnet ef migrations` at design-time (no DI stack available there).
// Credentials must come from the environment — same variables as docker-compose / `.env`.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = GetDesignTimeConnectionString();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options, new SystemClock());
    }

    private static string GetDesignTimeConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var db = Environment.GetEnvironmentVariable("POSTGRES_DB");
        var user = Environment.GetEnvironmentVariable("POSTGRES_USER");
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

        if (string.IsNullOrWhiteSpace(db) || string.IsNullOrWhiteSpace(user) || password is null)
        {
            throw new InvalidOperationException(
                "Design-time EF Core needs database settings. Set ConnectionStrings__Default, " +
                "or POSTGRES_DB, POSTGRES_USER, and POSTGRES_PASSWORD in the environment " +
                "(e.g. export after loading `.env`, or set them in your shell before `dotnet ef`). " +
                "Optional: POSTGRES_HOST (default localhost), POSTGRES_PORT (default 5432).");
        }

        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var portStr = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        if (!int.TryParse(portStr, out var port))
            port = 5432;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = db,
            Username = user,
            Password = password,
        };

        return builder.ConnectionString;
    }
}
