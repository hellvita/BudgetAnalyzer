using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BudgetAnalyzer.IntegrationTests.Infrastructure;

namespace BudgetAnalyzer.IntegrationTests.ErrorHandling;

[Collection("Integration")]
public class ErrorHandlingTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"err-{Guid.NewGuid():N}@tests.budget.dev";

    public ErrorHandlingTests(BudgetApiFactory factory) : base(factory) { }

    // Test AB1 — Missing required field (400, model binding)
    [Fact]
    public async Task Register_MissingPassword_Returns400WithProblemDetails()
    {
        var content = new StringContent("""{"email":"missing@tests.budget.dev"}""", Encoding.UTF8, "application/json");
        var response = await Client.PostAsync("/api/auth/register", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(body));
    }

    // Test AB2 — Field fails annotation constraint (400, model binding)
    [Fact]
    public async Task Register_InvalidEmailAnnotation_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register",
            new { email = "bad-email", password = "ValidPass123!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Test AB3 — Service-level ValidationException (400)
    [Fact]
    public async Task SetLimit_NegativeAmount_Returns400ProblemDetails()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync("/api/limits/2026-01-01", new { amount = -50m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(400, body.Status);
    }

    // Test AB4 — NotFoundException (404) with ProblemDetails format
    [Fact]
    public async Task DeleteLimit_NonExistent_Returns404ProblemDetails()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.DeleteAsync("/api/limits/2099-01-01");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(404, body.Status);
    }

    // Test AB5 — ConflictException (409) with ProblemDetails format
    [Fact]
    public async Task Register_DuplicateEmail_Returns409ProblemDetails()
    {
        var email = UniqueEmail();
        await RegisterUserAsync(email);

        var response = await Client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "Password123!" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(409, body.Status);
    }

    // Test AB6 — No token on protected endpoint → 401
    [Fact]
    public async Task GetBudget_NoToken_Returns401()
    {
        var response = await Client.GetAsync("/api/me/budget");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Test AB7 — Content-Type header on error responses is application/problem+json
    [Fact]
    public async Task ErrorResponse_ServiceLevel_ContentTypeIsProblemJson()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync("/api/limits/2026-01-01", new { amount = -1m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ErrorResponse_NotFound_ContentTypeIsProblemJson()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.DeleteAsync("/api/limits/2099-12-31");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ErrorResponse_Conflict_ContentTypeIsProblemJson()
    {
        var email = UniqueEmail();
        await RegisterUserAsync(email);

        var response = await Client.PostAsJsonAsync("/api/auth/register", new { email, password = "Password123!" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private record ProblemDetailsResponse(string? Type, string? Title, int? Status, string? Detail);
}
