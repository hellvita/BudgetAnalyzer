using System.Net;
using System.Net.Http.Json;
using BudgetAnalyzer.IntegrationTests.Infrastructure;

namespace BudgetAnalyzer.IntegrationTests.Expenses;

[Collection("Integration")]
public class ExpensesTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"exp-{Guid.NewGuid():N}@tests.budget.dev";

    public ExpensesTests(BudgetApiFactory factory) : base(factory) { }

    private async Task<(HttpClient Client, Guid CategoryId)> SetupUserWithCategoryAsync()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat = await CreateCategoryAsync(client, "Food");
        return (client, cat.Id);
    }

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient client, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/categories", new { name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CategoryResponse>(JsonOptions))!;
    }

    // Test X1 — Upsert expense (insert path) → 204
    [Fact]
    public async Task UpsertExpense_NewEntry_Returns204()
    {
        var (client, catId) = await SetupUserWithCategoryAsync();
        var date = "2026-03-01";

        var response = await client.PutAsJsonAsync($"/api/expenses/{date}/{catId}", new { amount = 45.50m });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // Test X2 — Upsert same date+category (update path) — value replaced
    [Fact]
    public async Task UpsertExpense_SameDateCategory_UpdatesAmount()
    {
        var (client, catId) = await SetupUserWithCategoryAsync();
        var date = "2026-03-02";

        await client.PutAsJsonAsync($"/api/expenses/{date}/{catId}", new { amount = 10m });
        await client.PutAsJsonAsync($"/api/expenses/{date}/{catId}", new { amount = 99m });

        var body = await (await client.GetAsync($"/api/expenses/by-date/{date}"))
            .Content.ReadFromJsonAsync<ExpenseByDateResponse>(JsonOptions);

        var entry = body!.PerCategory.Single(e => e.CategoryId == catId);
        Assert.Equal(99m, entry.Amount);
    }

    // Test X3 — Upsert for second category on same date
    [Fact]
    public async Task UpsertExpense_TwoCategoriesSameDate_BothStored()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat1 = await CreateCategoryAsync(client, "Transport");
        var cat2 = await CreateCategoryAsync(client, "Entertainment");
        var date = "2026-03-03";

        await client.PutAsJsonAsync($"/api/expenses/{date}/{cat1.Id}", new { amount = 20m });
        await client.PutAsJsonAsync($"/api/expenses/{date}/{cat2.Id}", new { amount = 35m });

        var body = await (await client.GetAsync($"/api/expenses/by-date/{date}"))
            .Content.ReadFromJsonAsync<ExpenseByDateResponse>(JsonOptions);

        Assert.Equal(55m, body!.DailyTotal);
    }

    // Test X4 — Upsert on a different date
    [Fact]
    public async Task UpsertExpense_DifferentDates_StoredSeparately()
    {
        var (client, catId) = await SetupUserWithCategoryAsync();

        await client.PutAsJsonAsync($"/api/expenses/2026-03-10/{catId}", new { amount = 11m });
        await client.PutAsJsonAsync($"/api/expenses/2026-03-11/{catId}", new { amount = 22m });

        var day10 = await (await client.GetAsync("/api/expenses/by-date/2026-03-10"))
            .Content.ReadFromJsonAsync<ExpenseByDateResponse>(JsonOptions);
        var day11 = await (await client.GetAsync("/api/expenses/by-date/2026-03-11"))
            .Content.ReadFromJsonAsync<ExpenseByDateResponse>(JsonOptions);

        Assert.Equal(11m, day10!.PerCategory.Single(e => e.CategoryId == catId).Amount);
        Assert.Equal(22m, day11!.PerCategory.Single(e => e.CategoryId == catId).Amount);
    }

    // Test X5 — GET by-date with entries
    [Fact]
    public async Task GetByDate_WithEntries_ReturnsCorrectAmounts()
    {
        var (client, catId) = await SetupUserWithCategoryAsync();
        var date = "2026-04-05";
        await client.PutAsJsonAsync($"/api/expenses/{date}/{catId}", new { amount = 77.25m });

        var response = await client.GetAsync($"/api/expenses/by-date/{date}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ExpenseByDateResponse>(JsonOptions);
        Assert.NotNull(body);
        var entry = body.PerCategory.Single(e => e.CategoryId == catId);
        Assert.Equal(77.25m, entry.Amount);
        Assert.Equal(77.25m, body.DailyTotal);
    }

    // Test X6 — GET by-date with no entries — returns zero for active categories
    [Fact]
    public async Task GetByDate_NoEntries_ReturnsZeroForActiveCategories()
    {
        var (client, catId) = await SetupUserWithCategoryAsync();

        var response = await client.GetAsync("/api/expenses/by-date/2026-01-01");
        var body = await response.Content.ReadFromJsonAsync<ExpenseByDateResponse>(JsonOptions);

        Assert.NotNull(body);
        Assert.Equal(0m, body.DailyTotal);
        Assert.Contains(body.PerCategory, c => c.CategoryId == catId && c.Amount == 0m);
    }

    // Test X7 — GET by-month
    [Fact]
    public async Task GetByMonth_WithEntries_ReturnsDaysInMonth()
    {
        var (client, catId) = await SetupUserWithCategoryAsync();

        await client.PutAsJsonAsync($"/api/expenses/2026-04-01/{catId}", new { amount = 50m });
        await client.PutAsJsonAsync($"/api/expenses/2026-04-15/{catId}", new { amount = 75m });

        var response = await client.GetAsync("/api/expenses/by-month/2026-04");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ExpenseByMonthDayItem>>(JsonOptions);
        Assert.Equal(30, body!.Count);

        var day1 = body.Single(d => d.Date == new DateOnly(2026, 4, 1));
        var day15 = body.Single(d => d.Date == new DateOnly(2026, 4, 15));

        Assert.Equal(50m, day1.DailyTotal);
        Assert.Equal(75m, day15.DailyTotal);
    }

    // Test X8 — DELETE an expense
    [Fact]
    public async Task DeleteExpense_Existing_Returns204()
    {
        var (client, catId) = await SetupUserWithCategoryAsync();
        var date = "2026-05-05";
        await client.PutAsJsonAsync($"/api/expenses/{date}/{catId}", new { amount = 30m });

        var deleteResponse = await client.DeleteAsync($"/api/expenses/{date}/{catId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    // Test X9 — GET by-date reflects deletion
    [Fact]
    public async Task GetByDate_AfterDelete_ReturnsZero()
    {
        var (client, catId) = await SetupUserWithCategoryAsync();
        var date = "2026-05-06";
        await client.PutAsJsonAsync($"/api/expenses/{date}/{catId}", new { amount = 50m });
        await client.DeleteAsync($"/api/expenses/{date}/{catId}");

        var body = await (await client.GetAsync($"/api/expenses/by-date/{date}"))
            .Content.ReadFromJsonAsync<ExpenseByDateResponse>(JsonOptions);

        Assert.Equal(0m, body!.PerCategory.Single(e => e.CategoryId == catId).Amount);
    }

    // Test X10 — DELETE non-existent expense → 404
    [Fact]
    public async Task DeleteExpense_NonExistent_Returns404()
    {
        var (client, catId) = await SetupUserWithCategoryAsync();

        var response = await client.DeleteAsync($"/api/expenses/2026-12-31/{catId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Test X11 — Negative amount → 400
    [Fact]
    public async Task UpsertExpense_NegativeAmount_Returns400()
    {
        var (client, catId) = await SetupUserWithCategoryAsync();

        var response = await client.PutAsJsonAsync($"/api/expenses/2026-03-20/{catId}", new { amount = -5m });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Test X12 — Zero amount is allowed
    [Fact]
    public async Task UpsertExpense_ZeroAmount_Returns204()
    {
        var (client, catId) = await SetupUserWithCategoryAsync();

        var response = await client.PutAsJsonAsync($"/api/expenses/2026-03-21/{catId}", new { amount = 0m });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // Test X13 — Expense on archived category → 400
    [Fact]
    public async Task UpsertExpense_ArchivedCategory_Returns400()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat = await CreateCategoryAsync(client, "ToBeArchived");
        await client.PostAsync($"/api/categories/{cat.Id}/archive", null);

        var response = await client.PutAsJsonAsync($"/api/expenses/2026-03-22/{cat.Id}", new { amount = 10m });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Test X14 — Expense for unknown category → 404
    [Fact]
    public async Task UpsertExpense_UnknownCategory_Returns404()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync($"/api/expenses/2026-03-23/{Guid.NewGuid()}", new { amount = 10m });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Test X15 — Expense isolation between users
    [Fact]
    public async Task Expenses_AreIsolatedPerUser()
    {
        var (tokenA, _) = await RegisterUserAsync(UniqueEmail());
        var (tokenB, _) = await RegisterUserAsync(UniqueEmail());
        var clientA = CreateAuthenticatedClient(tokenA);
        var clientB = CreateAuthenticatedClient(tokenB);

        var catA = await CreateCategoryAsync(clientA, "UserACategory");
        var date = "2026-06-15";
        await clientA.PutAsJsonAsync($"/api/expenses/{date}/{catA.Id}", new { amount = 100m });

        // User B has no categories → by-date returns empty perCategory
        var bodyB = await (await clientB.GetAsync($"/api/expenses/by-date/{date}"))
            .Content.ReadFromJsonAsync<ExpenseByDateResponse>(JsonOptions);

        Assert.Equal(0m, bodyB!.DailyTotal);
        Assert.DoesNotContain(bodyB.PerCategory, e => e.CategoryId == catA.Id);
    }

    private record CategoryResponse(Guid Id, string Name, bool IsArchived);
    private record ExpenseByCategoryItem(Guid CategoryId, string CategoryName, decimal Amount);
    private record ExpenseByDateResponse(DateOnly Date, List<ExpenseByCategoryItem> PerCategory, decimal DailyTotal);
    private record ExpenseMonthPerCategory(Guid CategoryId, decimal Amount);
    private record ExpenseByMonthDayItem(DateOnly Date, List<ExpenseMonthPerCategory> PerCategory, decimal DailyTotal);
}
