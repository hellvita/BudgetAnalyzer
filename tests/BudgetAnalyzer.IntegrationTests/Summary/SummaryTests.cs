using System.Net;
using System.Net.Http.Json;
using BudgetAnalyzer.IntegrationTests.Infrastructure;

namespace BudgetAnalyzer.IntegrationTests.Summary;

[Collection("Integration")]
public class SummaryTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"sum-{Guid.NewGuid():N}@tests.budget.dev";

    public SummaryTests(BudgetApiFactory factory) : base(factory) { }

    private static async Task<CategoryDto> CreateCategoryAsync(HttpClient client, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/categories", new { name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions))!;
    }

    // Test AA1 — GET day summary with no data → all zeros
    [Fact]
    public async Task GetDaySummary_NoData_ReturnsAllZeros()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/summary/day/2026-01-01");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DaySummaryResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(0m, body.Income);
        Assert.Equal(0m, body.TotalExpenses);
        Assert.Null(body.EffectiveLimit);
        Assert.Null(body.LimitDiff);
        Assert.Equal(0m, body.Net);
    }

    // Test AA2 — GET day summary with expenses and income
    [Fact]
    public async Task GetDaySummary_WithExpensesAndIncome_ReturnsCorrectValues()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat = await CreateCategoryAsync(client, "Groceries");
        var date = "2026-08-15";

        await client.PutAsJsonAsync($"/api/expenses/{date}/{cat.Id}", new { amount = 120m });
        await client.PutAsJsonAsync($"/api/incomes/{date}", new { amount = 500m });
        await client.PutAsJsonAsync($"/api/limits/2026-08-01", new { amount = 200m });

        var body = await (await client.GetAsync($"/api/summary/day/{date}"))
            .Content.ReadFromJsonAsync<DaySummaryResponse>(JsonOptions);

        Assert.Equal(500m, body!.Income);
        Assert.Equal(120m, body.TotalExpenses);
        Assert.Equal(200m, body.EffectiveLimit);
        Assert.Equal(80m, body.LimitDiff);  // 200 - 120
        Assert.Equal(380m, body.Net);       // 500 - 120
    }

    // Test AA3 — GET day summary with no limit set → limitDiff is null
    [Fact]
    public async Task GetDaySummary_NoLimit_LimitDiffIsNull()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat = await CreateCategoryAsync(client, "Expenses");
        var date = "2026-09-10";

        await client.PutAsJsonAsync($"/api/expenses/{date}/{cat.Id}", new { amount = 50m });

        var body = await (await client.GetAsync($"/api/summary/day/{date}"))
            .Content.ReadFromJsonAsync<DaySummaryResponse>(JsonOptions);

        Assert.Null(body!.EffectiveLimit);
        Assert.Null(body.LimitDiff);
        Assert.Equal(50m, body.TotalExpenses);
    }

    // Test AA4 — GET month summary basic
    [Fact]
    public async Task GetMonthSummary_BasicData_ReturnsCorrectTotals()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat = await CreateCategoryAsync(client, "Rent");

        await client.PutAsJsonAsync("/api/expenses/2026-10-05/"+cat.Id, new { amount = 800m });
        await client.PutAsJsonAsync("/api/expenses/2026-10-20/"+cat.Id, new { amount = 200m });
        await client.PutAsJsonAsync("/api/incomes/2026-10-01", new { amount = 3000m });
        await client.PutAsJsonAsync("/api/limits/2026-10-01", new { amount = 50m });

        var body = await (await client.GetAsync("/api/summary/month/2026-10"))
            .Content.ReadFromJsonAsync<MonthSummaryResponse>(JsonOptions);

        Assert.NotNull(body);
        Assert.Equal(31, body.Days.Count);
        Assert.Equal(1000m, body.MonthTotals.TotalExpenses);
        Assert.Equal(3000m, body.MonthTotals.TotalIncome);
        Assert.Equal(2000m, body.MonthTotals.Net); // 3000 - 1000
        // allowedMonthlyBudget = 50 * 31 = 1550
        Assert.Equal(1550m, body.MonthTotals.AllowedMonthlyBudget);
    }

    // Test AA5 — GET month summary with mid-month limit change
    [Fact]
    public async Task GetMonthSummary_MidMonthLimitChange_AllowedBudgetCorrect()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        // First 14 days at 100, last 17 days at 200 (November has 30 days)
        await client.PutAsJsonAsync("/api/limits/2026-11-01", new { amount = 100m });
        await client.PutAsJsonAsync("/api/limits/2026-11-15", new { amount = 200m });

        var body = await (await client.GetAsync("/api/summary/month/2026-11"))
            .Content.ReadFromJsonAsync<MonthSummaryResponse>(JsonOptions);

        // Nov 1-14 = 14 days * 100 = 1400
        // Nov 15-30 = 16 days * 200 = 3200
        var expected = 14 * 100m + 16 * 200m;
        Assert.Equal(expected, body!.MonthTotals.AllowedMonthlyBudget);
    }

    // Test AA6 — GET all-time summary empty account
    [Fact]
    public async Task GetAllTimeSummary_EmptyAccount_ReturnsAllZeros()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var body = await (await client.GetAsync("/api/summary/all-time"))
            .Content.ReadFromJsonAsync<AllTimeSummaryResponse>(JsonOptions);

        Assert.NotNull(body);
        Assert.Equal(0m, body.InitialBudget);
        Assert.Equal(0m, body.TotalIncome);
        Assert.Equal(0m, body.TotalExpenses);
        Assert.Equal(0m, body.CurrentBalance);
        Assert.Equal(0m, body.Net);
    }

    // Test AA7 — GET all-time summary with data
    [Fact]
    public async Task GetAllTimeSummary_WithData_ReturnsCorrectBalance()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat = await CreateCategoryAsync(client, "Bills");

        // Set initial budget
        await client.PutAsJsonAsync("/api/me/budget", new { initialBudget = 500m });

        // Add income and expenses
        await client.PutAsJsonAsync("/api/incomes/2026-12-01", new { amount = 2000m });
        await client.PutAsJsonAsync($"/api/expenses/2026-12-01/{cat.Id}", new { amount = 300m });

        var body = await (await client.GetAsync("/api/summary/all-time"))
            .Content.ReadFromJsonAsync<AllTimeSummaryResponse>(JsonOptions);

        Assert.Equal(500m, body!.InitialBudget);
        Assert.Equal(2000m, body.TotalIncome);
        Assert.Equal(300m, body.TotalExpenses);
        Assert.Equal(2200m, body.CurrentBalance); // 500 + 2000 - 300
        Assert.Equal(1700m, body.Net);            // 2000 - 300
    }

    // Test AA8 — Summary scoped per user
    [Fact]
    public async Task Summary_IsScopedPerUser()
    {
        var (tokenA, _) = await RegisterUserAsync(UniqueEmail());
        var (tokenB, _) = await RegisterUserAsync(UniqueEmail());
        var clientA = CreateAuthenticatedClient(tokenA);
        var clientB = CreateAuthenticatedClient(tokenB);

        var catA = await CreateCategoryAsync(clientA, "UserACat");
        await clientA.PutAsJsonAsync("/api/me/budget", new { initialBudget = 1000m });
        await clientA.PutAsJsonAsync("/api/incomes/2026-12-15", new { amount = 5000m });
        await clientA.PutAsJsonAsync($"/api/expenses/2026-12-15/{catA.Id}", new { amount = 200m });

        var bodyB = await (await clientB.GetAsync("/api/summary/all-time"))
            .Content.ReadFromJsonAsync<AllTimeSummaryResponse>(JsonOptions);

        Assert.Equal(0m, bodyB!.TotalIncome);
        Assert.Equal(0m, bodyB.TotalExpenses);
        Assert.Equal(0m, bodyB.InitialBudget);
    }

    // Unauthenticated → 401
    [Fact]
    public async Task GetDaySummary_NoToken_Returns401()
    {
        var response = await Client.GetAsync("/api/summary/day/2026-01-01");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMonthSummary_NoToken_Returns401()
    {
        var response = await Client.GetAsync("/api/summary/month/2026-01");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllTimeSummary_NoToken_Returns401()
    {
        var response = await Client.GetAsync("/api/summary/all-time");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record CategoryDto(Guid Id, string Name, bool IsArchived);
    private record DaySummaryResponse(DateOnly Date, decimal Income, decimal TotalExpenses, decimal? EffectiveLimit, decimal? LimitDiff, decimal Net);
    private record MonthSummaryDayItem(DateOnly Date, decimal TotalExpenses, decimal TotalIncome, decimal? EffectiveLimit, decimal? LimitDiff, decimal Net);
    private record MonthTotals(decimal TotalExpenses, decimal TotalIncome, decimal AllowedMonthlyBudget, decimal TotalLimitDiff, decimal Net);
    private record MonthSummaryResponse(int Year, int Month, List<MonthSummaryDayItem> Days, MonthTotals MonthTotals);
    private record AllTimeSummaryResponse(decimal InitialBudget, decimal TotalIncome, decimal TotalExpenses, decimal TotalLimitDiff, decimal CurrentBalance, decimal Net);
}
