using System.Net;
using System.Net.Http.Json;
using BudgetAnalyzer.IntegrationTests.Infrastructure;

namespace BudgetAnalyzer.IntegrationTests.Incomes;

[Collection("Integration")]
public class IncomesTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"inc-{Guid.NewGuid():N}@tests.budget.dev";

    public IncomesTests(BudgetApiFactory factory) : base(factory) { }

    // Test Y1 — Upsert income (insert path) → 204
    [Fact]
    public async Task UpsertIncome_NewEntry_Returns204()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync("/api/incomes/2026-03-01", new { amount = 3000m });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // Test Y2 — Upsert same date (update path)
    [Fact]
    public async Task UpsertIncome_SameDate_UpdatesAmount()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var date = "2026-03-05";

        await client.PutAsJsonAsync($"/api/incomes/{date}", new { amount = 1000m });
        await client.PutAsJsonAsync($"/api/incomes/{date}", new { amount = 2500m });

        var body = await (await client.GetAsync("/api/incomes/by-month/2026-03"))
            .Content.ReadFromJsonAsync<List<IncomeDayItem>>(JsonOptions);

        var day = body!.Single(d => d.Date == new DateOnly(2026, 3, 5));
        Assert.Equal(2500m, day.Amount);
    }

    // Test Y3 — GET by-month
    [Fact]
    public async Task GetByMonth_WithEntries_ReturnsDaysInMonth()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        await client.PutAsJsonAsync("/api/incomes/2026-04-10", new { amount = 500m });
        await client.PutAsJsonAsync("/api/incomes/2026-04-20", new { amount = 800m });

        var response = await client.GetAsync("/api/incomes/by-month/2026-04");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<IncomeDayItem>>(JsonOptions);
        Assert.Equal(30, body!.Count);

        Assert.Equal(500m, body.Single(d => d.Date == new DateOnly(2026, 4, 10)).Amount);
        Assert.Equal(800m, body.Single(d => d.Date == new DateOnly(2026, 4, 20)).Amount);
        Assert.Equal(0m, body.Single(d => d.Date == new DateOnly(2026, 4, 1)).Amount);
    }

    // Test Y4 — DELETE income
    [Fact]
    public async Task DeleteIncome_Existing_Returns204()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var date = "2026-04-25";
        await client.PutAsJsonAsync($"/api/incomes/{date}", new { amount = 100m });

        var response = await client.DeleteAsync($"/api/incomes/{date}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // Test Y4 — After deletion, amount is 0
    [Fact]
    public async Task GetByMonth_AfterDelete_ReturnsZeroForDeletedDate()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var date = "2026-05-10";
        await client.PutAsJsonAsync($"/api/incomes/{date}", new { amount = 400m });
        await client.DeleteAsync($"/api/incomes/{date}");

        var body = await (await client.GetAsync("/api/incomes/by-month/2026-05"))
            .Content.ReadFromJsonAsync<List<IncomeDayItem>>(JsonOptions);

        Assert.Equal(0m, body!.Single(d => d.Date == new DateOnly(2026, 5, 10)).Amount);
    }

    // Test Y5 — DELETE non-existent income → 404
    [Fact]
    public async Task DeleteIncome_NonExistent_Returns404()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.DeleteAsync("/api/incomes/2026-12-31");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Test Y6 — Negative amount → 400
    [Fact]
    public async Task UpsertIncome_NegativeAmount_Returns400()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync("/api/incomes/2026-03-15", new { amount = -500m });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Test Y7 — Zero amount is allowed
    [Fact]
    public async Task UpsertIncome_ZeroAmount_Returns204()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync("/api/incomes/2026-03-16", new { amount = 0m });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // Test Y8 — Income isolation between users
    [Fact]
    public async Task Incomes_AreIsolatedPerUser()
    {
        var (tokenA, _) = await RegisterUserAsync(UniqueEmail());
        var (tokenB, _) = await RegisterUserAsync(UniqueEmail());
        var clientA = CreateAuthenticatedClient(tokenA);
        var clientB = CreateAuthenticatedClient(tokenB);
        var date = "2026-07-01";

        await clientA.PutAsJsonAsync($"/api/incomes/{date}", new { amount = 5000m });

        var bodyB = await (await clientB.GetAsync("/api/incomes/by-month/2026-07"))
            .Content.ReadFromJsonAsync<List<IncomeDayItem>>(JsonOptions);

        Assert.Equal(0m, bodyB!.Single(d => d.Date == new DateOnly(2026, 7, 1)).Amount);
    }

    // Unauthenticated access → 401
    [Fact]
    public async Task GetByMonth_NoToken_Returns401()
    {
        var response = await Client.GetAsync("/api/incomes/by-month/2026-03");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record IncomeDayItem(DateOnly Date, decimal Amount);
}
