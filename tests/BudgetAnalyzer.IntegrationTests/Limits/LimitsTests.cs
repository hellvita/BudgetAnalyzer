using System.Net;
using System.Net.Http.Json;
using BudgetAnalyzer.IntegrationTests.Infrastructure;

namespace BudgetAnalyzer.IntegrationTests.Limits;

[Collection("Integration")]
public class LimitsTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"lim-{Guid.NewGuid():N}@tests.budget.dev";

    public LimitsTests(BudgetApiFactory factory) : base(factory) { }

    // Test Z1 — GET history empty on fresh account
    [Fact]
    public async Task GetHistory_FreshAccount_ReturnsEmpty()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/limits");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<LimitHistoryItem>>(JsonOptions);
        Assert.Empty(body!);
    }

    // Test Z2 — PUT a limit (insert path) → 204
    [Fact]
    public async Task SetLimit_NewDate_Returns204()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync("/api/limits/2026-01-01", new { amount = 100m });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // Test Z3 — GET history populated
    [Fact]
    public async Task GetHistory_AfterSet_ReturnsEntry()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        await client.PutAsJsonAsync("/api/limits/2026-01-01", new { amount = 150m });

        var body = await (await client.GetAsync("/api/limits"))
            .Content.ReadFromJsonAsync<List<LimitHistoryItem>>(JsonOptions);

        Assert.Single(body!);
        Assert.Equal(new DateOnly(2026, 1, 1), body![0].EffectiveFromDate);
        Assert.Equal(150m, body[0].Amount);
    }

    // Test Z4 — PUT same date (update path)
    [Fact]
    public async Task SetLimit_SameDate_UpdatesAmount()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        await client.PutAsJsonAsync("/api/limits/2026-02-01", new { amount = 80m });
        await client.PutAsJsonAsync("/api/limits/2026-02-01", new { amount = 120m });

        var body = await (await client.GetAsync("/api/limits"))
            .Content.ReadFromJsonAsync<List<LimitHistoryItem>>(JsonOptions);

        Assert.Single(body!);
        Assert.Equal(120m, body![0].Amount);
    }

    // Test Z5 — PUT a second date (mid-period change)
    [Fact]
    public async Task SetLimit_TwoDates_BothInHistory()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        await client.PutAsJsonAsync("/api/limits/2026-03-01", new { amount = 50m });
        await client.PutAsJsonAsync("/api/limits/2026-03-15", new { amount = 75m });

        var body = await (await client.GetAsync("/api/limits"))
            .Content.ReadFromJsonAsync<List<LimitHistoryItem>>(JsonOptions);

        Assert.Equal(2, body!.Count);
    }

    // Test Z6 — Effective limit queries verified via summary day endpoint
    [Fact]
    public async Task EffectiveLimit_BeforeFirstEntry_IsNull()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        await client.PutAsJsonAsync("/api/limits/2026-06-10", new { amount = 200m });

        // Query a date BEFORE the first limit entry
        var daySummary = await (await client.GetAsync("/api/summary/day/2026-06-09"))
            .Content.ReadFromJsonAsync<DaySummaryResponse>(JsonOptions);

        Assert.Null(daySummary!.EffectiveLimit);
        Assert.Null(daySummary.LimitDiff);
    }

    [Fact]
    public async Task EffectiveLimit_OnOrAfterEffectiveDate_IsApplied()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        await client.PutAsJsonAsync("/api/limits/2026-06-10", new { amount = 200m });

        var daySummary = await (await client.GetAsync("/api/summary/day/2026-06-10"))
            .Content.ReadFromJsonAsync<DaySummaryResponse>(JsonOptions);

        Assert.Equal(200m, daySummary!.EffectiveLimit);
    }

    [Fact]
    public async Task EffectiveLimit_MidPeriodChange_CorrectLimitPerDay()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        await client.PutAsJsonAsync("/api/limits/2026-07-01", new { amount = 100m });
        await client.PutAsJsonAsync("/api/limits/2026-07-15", new { amount = 200m });

        var day14 = await (await client.GetAsync("/api/summary/day/2026-07-14"))
            .Content.ReadFromJsonAsync<DaySummaryResponse>(JsonOptions);
        var day15 = await (await client.GetAsync("/api/summary/day/2026-07-15"))
            .Content.ReadFromJsonAsync<DaySummaryResponse>(JsonOptions);

        Assert.Equal(100m, day14!.EffectiveLimit);
        Assert.Equal(200m, day15!.EffectiveLimit);
    }

    // Test Z7 — DELETE a limit entry
    [Fact]
    public async Task DeleteLimit_Existing_Returns204()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        await client.PutAsJsonAsync("/api/limits/2026-08-01", new { amount = 90m });

        var response = await client.DeleteAsync("/api/limits/2026-08-01");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var history = await (await client.GetAsync("/api/limits"))
            .Content.ReadFromJsonAsync<List<LimitHistoryItem>>(JsonOptions);
        Assert.Empty(history!);
    }

    // Test Z8 — DELETE non-existent limit → 404
    [Fact]
    public async Task DeleteLimit_NonExistent_Returns404()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.DeleteAsync("/api/limits/2026-12-31");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Test Z9 — Negative amount → 400
    [Fact]
    public async Task SetLimit_NegativeAmount_Returns400()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync("/api/limits/2026-09-01", new { amount = -10m });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Test Z10 — Zero amount is allowed
    [Fact]
    public async Task SetLimit_ZeroAmount_Returns204()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync("/api/limits/2026-09-02", new { amount = 0m });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // Test Z11 — Limit isolation between users
    [Fact]
    public async Task Limits_AreIsolatedPerUser()
    {
        var (tokenA, _) = await RegisterUserAsync(UniqueEmail());
        var (tokenB, _) = await RegisterUserAsync(UniqueEmail());
        var clientA = CreateAuthenticatedClient(tokenA);
        var clientB = CreateAuthenticatedClient(tokenB);

        await clientA.PutAsJsonAsync("/api/limits/2026-10-01", new { amount = 300m });

        var historyB = await (await clientB.GetAsync("/api/limits"))
            .Content.ReadFromJsonAsync<List<LimitHistoryItem>>(JsonOptions);

        Assert.Empty(historyB!);
    }

    // Unauthenticated → 401
    [Fact]
    public async Task GetLimits_NoToken_Returns401()
    {
        var response = await Client.GetAsync("/api/limits");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record LimitHistoryItem(DateOnly EffectiveFromDate, decimal Amount);
    private record DaySummaryResponse(DateOnly Date, decimal Income, decimal TotalExpenses, decimal? EffectiveLimit, decimal? LimitDiff, decimal Net);
}
