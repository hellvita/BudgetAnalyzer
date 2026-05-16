using System.Net;
using BudgetAnalyzer.Infrastructure.Persistence;
using BudgetAnalyzer.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetAnalyzer.IntegrationTests.Auth;

[Collection("Integration")]
public class LogoutTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"logout-{Guid.NewGuid():N}@tests.budget.dev";

    public LogoutTests(BudgetApiFactory factory) : base(factory) { }

    // LO1 — Logout with valid token → 204
    [Fact]
    public async Task Logout_ValidToken_Returns204()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // LO2 — Token rejected on subsequent request after logout
    [Fact]
    public async Task Logout_TokenRejectedOnSubsequentRequest()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        await client.PostAsync("/api/auth/logout", null);

        var pingResponse = await client.GetAsync("/api/ping");
        Assert.Equal(HttpStatusCode.Unauthorized, pingResponse.StatusCode);
    }

    // LO3 — Logout without token → 401
    [Fact]
    public async Task Logout_NoToken_Returns401()
    {
        var response = await Client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // LO4 — Logout twice with same token: second call returns 401
    [Fact]
    public async Task Logout_SecondLogoutWithSameToken_Returns401()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        await client.PostAsync("/api/auth/logout", null);

        var retryResponse = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, retryResponse.StatusCode);
    }

    // LO5 — JTI is stored in revoked_tokens table after logout
    [Fact]
    public async Task Logout_JtiStoredInRevokedTokensTable()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var jti = GetJtiFromToken(token);

        await client.PostAsync("/api/auth/logout", null);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.RevokedTokens.AnyAsync(t => t.Jti == jti));
    }

    // LO6 — Logging out one user does not invalidate another user's token
    [Fact]
    public async Task Logout_OtherUsersToken_NotAffected()
    {
        var (tokenA, _) = await RegisterUserAsync(UniqueEmail());
        var (tokenB, _) = await RegisterUserAsync(UniqueEmail());
        var clientA = CreateAuthenticatedClient(tokenA);
        var clientB = CreateAuthenticatedClient(tokenB);

        await clientA.PostAsync("/api/auth/logout", null);

        var pingResponse = await clientB.GetAsync("/api/ping");
        Assert.Equal(HttpStatusCode.OK, pingResponse.StatusCode);
    }

    // LO7 — After logout, a fresh login with same credentials issues a valid new token
    [Fact]
    public async Task Logout_FreshLoginAfterLogout_IssuesNewValidToken()
    {
        var email = UniqueEmail();
        var (token, _) = await RegisterUserAsync(email);
        var client = CreateAuthenticatedClient(token);

        await client.PostAsync("/api/auth/logout", null);

        var newToken = await LoginAsync(email);
        var newClient = CreateAuthenticatedClient(newToken);
        var pingResponse = await newClient.GetAsync("/api/ping");
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
