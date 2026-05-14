using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BudgetAnalyzer.IntegrationTests.Infrastructure;

namespace BudgetAnalyzer.IntegrationTests.Auth;

[Collection("Integration")]
public class AuthTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"auth-{Guid.NewGuid():N}@tests.budget.dev";

    public AuthTests(BudgetApiFactory factory) : base(factory) { }

    // Test A — Register a new user → 201 with token
    [Fact]
    public async Task Register_ValidCredentials_Returns201WithToken()
    {
        var email = UniqueEmail();

        var response = await Client.PostAsJsonAsync("/api/auth/register", new { email, password = "SecurePass1!" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.StartsWith("eyJ", body.Token);
        Assert.True(body.ExpiresAt > DateTime.UtcNow);
    }

    // Test A — email is normalised to lowercase
    [Fact]
    public async Task Register_MixedCaseEmail_StoresLowercase()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var email = $"AUTH-{uid}@TESTS.BUDGET.DEV";

        var (token, _) = await RegisterUserAsync(email);

        var parts = token.Split('.');
        var payload = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var doc = JsonDocument.Parse(json);
        var claimEmail = doc.RootElement.GetProperty("email").GetString();

        Assert.Equal(email.ToLowerInvariant(), claimEmail);
    }

    // Test B — Duplicate registration → 409
    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var email = UniqueEmail();
        await RegisterUserAsync(email);

        var response = await Client.PostAsJsonAsync("/api/auth/register", new { email, password = "SecurePass1!" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Test B — Duplicate with different casing still 409
    [Fact]
    public async Task Register_DuplicateEmailDifferentCase_Returns409()
    {
        var email = UniqueEmail();
        await RegisterUserAsync(email);

        var response = await Client.PostAsJsonAsync("/api/auth/register",
            new { email = email.ToUpperInvariant(), password = "SecurePass1!" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Test C1 — Invalid email format → 400
    [Fact]
    public async Task Register_InvalidEmailFormat_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register",
            new { email = "not-an-email", password = "SecurePass1!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Test C2 — Password too short → 400
    [Fact]
    public async Task Register_PasswordTooShort_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register",
            new { email = UniqueEmail(), password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Test C3 — Missing fields → 400
    [Fact]
    public async Task Register_MissingFields_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Test D — Login successfully → 200 with token
    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        var email = UniqueEmail();
        await RegisterUserAsync(email);

        var response = await Client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthTokenResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));
        Assert.StartsWith("eyJ", body.Token);
    }

    // Test E1 — Wrong password → 404 (generic, no user enumeration)
    [Fact]
    public async Task Login_WrongPassword_Returns404()
    {
        var email = UniqueEmail();
        await RegisterUserAsync(email);

        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Test E2 — Unknown email → 404 (same message)
    [Fact]
    public async Task Login_UnknownEmail_Returns404()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@tests.budget.dev", password = "Password123!" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Test G1 — No token on protected endpoint → 401
    [Fact]
    public async Task ProtectedEndpoint_NoToken_Returns401()
    {
        var response = await Client.GetAsync("/api/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Test G2 — Valid token on protected endpoint → 200
    [Fact]
    public async Task ProtectedEndpoint_ValidToken_Returns200()
    {
        var email = UniqueEmail();
        var (token, _) = await RegisterUserAsync(email);
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/api/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Test G3 — Tampered token → 401
    [Fact]
    public async Task ProtectedEndpoint_TamperedToken_Returns401()
    {
        var email = UniqueEmail();
        var (token, _) = await RegisterUserAsync(email);

        var tampered = token[..^5] + "XXXXX";
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync("/api/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // JWT claims verification
    [Fact]
    public async Task Register_Token_ContainsCorrectClaims()
    {
        var email = UniqueEmail();
        var (token, userId) = await RegisterUserAsync(email);

        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        var payload = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(userId.ToString(), doc.RootElement.GetProperty("sub").GetString());
        Assert.Equal("budget-analyzer", doc.RootElement.GetProperty("iss").GetString());
        Assert.True(doc.RootElement.TryGetProperty("exp", out _));
        Assert.True(doc.RootElement.TryGetProperty("jti", out _));
    }

}
