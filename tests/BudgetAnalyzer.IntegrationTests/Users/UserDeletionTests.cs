using System.Net;
using System.Net.Http.Json;
using BudgetAnalyzer.Infrastructure.Persistence;
using BudgetAnalyzer.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetAnalyzer.IntegrationTests.Users;

[Collection("Integration")]
public class UserDeletionTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"del-{Guid.NewGuid():N}@tests.budget.dev";

    public UserDeletionTests(BudgetApiFactory factory) : base(factory) { }

    [Fact]
    public async Task DeleteMe_Authenticated_Returns204()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.DeleteAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMe_Unauthenticated_Returns401()
    {
        var response = await Client.DeleteAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMe_FreshAccount_RemovesUserRow()
    {
        var (token, userId) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        await client.DeleteAsync("/api/users/me");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Id == userId));
    }

    [Fact]
    public async Task DeleteMe_WithAllDataTypes_RemovesEverything()
    {
        var (token, userId) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var catResponse = await client.PostAsJsonAsync("/api/categories", new { name = "Groceries" });
        var cat = await catResponse.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions);

        await client.PutAsJsonAsync($"/api/expenses/2026-05-01/{cat!.Id}", new { amount = 42.50m });
        await client.PutAsJsonAsync("/api/incomes/2026-05-01", new { amount = 200m });
        await client.PutAsJsonAsync("/api/limits/2026-01-01", new { amount = 75m });

        var deleteResponse = await client.DeleteAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.False(await db.Users.AnyAsync(u => u.Id == userId));
        Assert.False(await db.Categories.AnyAsync(c => c.UserId == userId));
        Assert.False(await db.DailyExpenses.AnyAsync(e => e.UserId == userId));
        Assert.False(await db.DailyIncomes.AnyAsync(i => i.UserId == userId));
        Assert.False(await db.DailyLimits.AnyAsync(l => l.UserId == userId));
    }

    [Fact]
    public async Task DeleteMe_SameEmailCanReRegister()
    {
        var email = UniqueEmail();
        var (token, _) = await RegisterUserAsync(email);
        var client = CreateAuthenticatedClient(token);

        await client.DeleteAsync("/api/users/me");

        var (newToken, _) = await RegisterUserAsync(email, "NewPass456!");

        Assert.False(string.IsNullOrWhiteSpace(newToken));
    }

    [Fact]
    public async Task DeleteMe_OldCredentialsNoLongerLogin()
    {
        var email = UniqueEmail();
        const string password = "Password123!";
        var (token, _) = await RegisterUserAsync(email, password);
        var client = CreateAuthenticatedClient(token);

        await client.DeleteAsync("/api/users/me");

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.NotFound, loginResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteMe_OtherUsersDataIsUntouched()
    {
        var (tokenA, _) = await RegisterUserAsync(UniqueEmail());
        var (tokenB, userBId) = await RegisterUserAsync(UniqueEmail());
        var clientA = CreateAuthenticatedClient(tokenA);
        var clientB = CreateAuthenticatedClient(tokenB);

        await clientB.PostAsJsonAsync("/api/categories", new { name = "Bills" });

        await clientA.DeleteAsync("/api/users/me");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Users.AnyAsync(u => u.Id == userBId));
        Assert.True(await db.Categories.AnyAsync(c => c.UserId == userBId));
    }

    private record CategoryDto(Guid Id, string Name, bool IsArchived);
}
