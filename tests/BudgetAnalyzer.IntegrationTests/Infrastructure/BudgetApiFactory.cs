using BudgetAnalyzer.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace BudgetAnalyzer.IntegrationTests.Infrastructure;

public class BudgetApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    // Stable test JWT secret — at least 32 chars for HMAC-SHA256
    private const string TestJwtKey = "test-budget-analyzer-signing-key-must-be-at-least-32-chars!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Environment variables are already set in InitializeAsync before the host builds
        builder.ConfigureServices(services =>
        {
            // Replace the registered DbContext so it points to the test container
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(opt =>
                opt.UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Default")!));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Set env vars BEFORE building the host (Services triggers the build)
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", "budget-analyzer");
        Environment.SetEnvironmentVariable("Jwt__Audience", "budget-analyzer-clients");
        Environment.SetEnvironmentVariable("Jwt__ExpiresMinutes", "60");

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", null);
        Environment.SetEnvironmentVariable("Jwt__Issuer", null);
        Environment.SetEnvironmentVariable("Jwt__Audience", null);
        Environment.SetEnvironmentVariable("Jwt__ExpiresMinutes", null);

        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<BudgetApiFactory> { }
