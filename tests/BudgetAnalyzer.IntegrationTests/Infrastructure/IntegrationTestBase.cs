using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BudgetAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetAnalyzer.IntegrationTests.Infrastructure;

[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly BudgetApiFactory Factory;
    protected readonly HttpClient Client;
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<Guid> _createdUserIds = new();

    protected IntegrationTestBase(BudgetApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        if (_createdUserIds.Count == 0) return;

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var userId in _createdUserIds)
        {
            // Delete in FK-safe order: expenses first (FK to categories), then incomes/limits, then categories, then users
            var categoryIds = await db.Categories
                .Where(c => c.UserId == userId)
                .Select(c => c.Id)
                .ToListAsync();

            if (categoryIds.Count > 0)
                await db.DailyExpenses.Where(e => categoryIds.Contains(e.CategoryId)).ExecuteDeleteAsync();

            await db.DailyExpenses.Where(e => e.UserId == userId).ExecuteDeleteAsync();
            await db.DailyIncomes.Where(i => i.UserId == userId).ExecuteDeleteAsync();
            await db.DailyLimits.Where(l => l.UserId == userId).ExecuteDeleteAsync();
            await db.Categories.Where(c => c.UserId == userId).ExecuteDeleteAsync();
            await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
        }
    }

    protected async Task<(string Token, Guid UserId)> RegisterUserAsync(string email, string password = "Password123!")
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new { email, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        var userId = GetUserIdFromToken(body!.Token);
        _createdUserIds.Add(userId);
        return (body.Token, userId);
    }

    protected async Task<string> LoginAsync(string email, string password = "Password123!")
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        return body!.Token;
    }

    protected HttpClient CreateAuthenticatedClient(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected static Guid GetUserIdFromToken(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return Guid.Empty;

        var payload = parts[1];
        var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("sub", out var sub)
            ? Guid.Parse(sub.GetString()!)
            : Guid.Empty;
    }

    protected record AuthTokenResponse(string Token, DateTime ExpiresAt);
}
