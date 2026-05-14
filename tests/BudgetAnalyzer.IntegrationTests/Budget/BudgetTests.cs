using System.Net;
using System.Net.Http.Json;
using BudgetAnalyzer.IntegrationTests.Infrastructure;

namespace BudgetAnalyzer.IntegrationTests.Budget;

[Collection("Integration")]
public class BudgetTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"budget-{Guid.NewGuid():N}@tests.budget.dev";

    public BudgetTests(BudgetApiFactory factory) : base(factory) { }

    // Test H — GET budget without token → 401
    [Fact]
    public async Task GetBudget_NoToken_Returns401()
    {
        var response = await Client.GetAsync("/api/me/budget");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Test I — GET budget after registration → default 0
    [Fact]
    public async Task GetBudget_FreshAccount_ReturnsZero()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/me/budget");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BudgetResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(0m, body.InitialBudget);
    }

    // Test J — PUT to set a new budget value
    [Fact]
    public async Task SetBudget_ValidValue_PersistsAndReturns()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var putResponse = await client.PutAsJsonAsync("/api/me/budget", new { initialBudget = 2500.00m });
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/me/budget");
        var body = await getResponse.Content.ReadFromJsonAsync<BudgetResponse>(JsonOptions);
        Assert.Equal(2500.00m, body!.InitialBudget);
    }

    // Test J — Zero is allowed
    [Fact]
    public async Task SetBudget_Zero_IsAllowed()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync("/api/me/budget", new { initialBudget = 0m });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Test K — PUT with negative value → 400
    [Fact]
    public async Task SetBudget_NegativeValue_Returns400()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync("/api/me/budget", new { initialBudget = -100m });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Test K — PUT with missing body → 400
    [Fact]
    public async Task SetBudget_MissingBody_Returns400()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync("/api/me/budget", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Test L — Budget is scoped per user
    [Fact]
    public async Task GetBudget_IsScopedPerUser()
    {
        var (tokenA, _) = await RegisterUserAsync(UniqueEmail());
        var (tokenB, _) = await RegisterUserAsync(UniqueEmail());
        var clientA = CreateAuthenticatedClient(tokenA);
        var clientB = CreateAuthenticatedClient(tokenB);

        await clientA.PutAsJsonAsync("/api/me/budget", new { initialBudget = 9999m });

        var responseB = await clientB.GetAsync("/api/me/budget");
        var bodyB = await responseB.Content.ReadFromJsonAsync<BudgetResponse>(JsonOptions);

        Assert.Equal(0m, bodyB!.InitialBudget);
    }

    private record BudgetResponse(decimal InitialBudget);
}
