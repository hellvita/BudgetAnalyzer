using System.Net;
using BudgetAnalyzer.Infrastructure.Persistence;
using BudgetAnalyzer.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetAnalyzer.IntegrationTests.Users;

[Collection("Integration")]
public class JwtInvalidationTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"jwt-{Guid.NewGuid():N}@tests.budget.dev";

    public JwtInvalidationTests(BudgetApiFactory factory) : base(factory) { }

    [Fact]
    public async Task DeleteMe_TokenRejectedOnSubsequentRequest()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var deleteResponse = await client.DeleteAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var pingResponse = await client.GetAsync("/api/ping");
        Assert.Equal(HttpStatusCode.Unauthorized, pingResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteMe_TokenRejectedOnDeleteRetry()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        await client.DeleteAsync("/api/users/me");

        var retryResponse = await client.DeleteAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, retryResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteMe_JtiStoredInRevokedTokensTable()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var jti = GetJtiFromToken(token);

        await client.DeleteAsync("/api/users/me");

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.RevokedTokens.AnyAsync(t => t.Jti == jti));
    }

    [Fact]
    public async Task OtherUsersToken_NotAffectedByDifferentUserDeletion()
    {
        var (tokenA, _) = await RegisterUserAsync(UniqueEmail());
        var (tokenB, _) = await RegisterUserAsync(UniqueEmail());
        var clientA = CreateAuthenticatedClient(tokenA);
        var clientB = CreateAuthenticatedClient(tokenB);

        await clientA.DeleteAsync("/api/users/me");

        var pingResponse = await clientB.GetAsync("/api/ping");
        Assert.Equal(HttpStatusCode.OK, pingResponse.StatusCode);
    }

    [Fact]
    public async Task ValidToken_BeforeDeletion_IsAccepted()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var pingResponse = await client.GetAsync("/api/ping");
        Assert.Equal(HttpStatusCode.OK, pingResponse.StatusCode);
    }

    private static string GetJtiFromToken(string token)
    {
        var parts = token.Split('.');
        var payload = parts[1];
        var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("jti").GetString()!;
    }
}
