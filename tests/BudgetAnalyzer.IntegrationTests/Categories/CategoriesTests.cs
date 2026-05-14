using System.Net;
using System.Net.Http.Json;
using BudgetAnalyzer.IntegrationTests.Infrastructure;

namespace BudgetAnalyzer.IntegrationTests.Categories;

[Collection("Integration")]
public class CategoriesTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"cat-{Guid.NewGuid():N}@tests.budget.dev";

    public CategoriesTests(BudgetApiFactory factory) : base(factory) { }

    // Test M — List categories empty on fresh account
    [Fact]
    public async Task ListCategories_FreshAccount_ReturnsEmpty()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.GetAsync("/api/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>(JsonOptions);
        Assert.Empty(body!);
    }

    // Test N — Create categories
    [Fact]
    public async Task CreateCategory_ValidName_Returns201()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/categories", new { name = "Groceries" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CategoryResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("Groceries", body.Name);
        Assert.False(body.IsArchived);
    }

    // Test O — List categories populated
    [Fact]
    public async Task ListCategories_AfterCreation_ReturnsThem()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        await client.PostAsJsonAsync("/api/categories", new { name = "Transport" });
        await client.PostAsJsonAsync("/api/categories", new { name = "Dining" });

        var response = await client.GetAsync("/api/categories");
        var body = await response.Content.ReadFromJsonAsync<List<CategoryResponse>>(JsonOptions);

        Assert.Equal(2, body!.Count);
    }

    // Test P — Duplicate name conflict → 409
    [Fact]
    public async Task CreateCategory_DuplicateName_Returns409()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        await client.PostAsJsonAsync("/api/categories", new { name = "Utilities" });
        var response = await client.PostAsJsonAsync("/api/categories", new { name = "Utilities" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Test Q — Rename a category
    [Fact]
    public async Task RenameCategory_ValidName_Returns200WithNewName()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var created = (await (await client.PostAsJsonAsync("/api/categories", new { name = "OldName" }))
            .Content.ReadFromJsonAsync<CategoryResponse>(JsonOptions))!;

        var renameResponse = await client.PutAsJsonAsync($"/api/categories/{created.Id}", new { name = "NewName" });

        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);
        var body = await renameResponse.Content.ReadFromJsonAsync<CategoryResponse>(JsonOptions);
        Assert.Equal("NewName", body!.Name);
    }

    // Test Q — Rename to conflicting name → 409
    [Fact]
    public async Task RenameCategory_ConflictingName_Returns409()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        await client.PostAsJsonAsync("/api/categories", new { name = "Existing" });
        var cat2 = (await (await client.PostAsJsonAsync("/api/categories", new { name = "ToRename" }))
            .Content.ReadFromJsonAsync<CategoryResponse>(JsonOptions))!;

        var response = await client.PutAsJsonAsync($"/api/categories/{cat2.Id}", new { name = "Existing" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Test R — Archive a category
    [Fact]
    public async Task ArchiveCategory_Succeeds_RemovesFromDefaultList()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var cat = (await (await client.PostAsJsonAsync("/api/categories", new { name = "ToArchive" }))
            .Content.ReadFromJsonAsync<CategoryResponse>(JsonOptions))!;

        var archiveResponse = await client.PostAsync($"/api/categories/{cat.Id}/archive", null);
        Assert.Equal(HttpStatusCode.NoContent, archiveResponse.StatusCode);

        var list = await (await client.GetAsync("/api/categories"))
            .Content.ReadFromJsonAsync<List<CategoryResponse>>(JsonOptions);
        Assert.DoesNotContain(list!, c => c.Id == cat.Id);
    }

    // Test S — List with includeArchived=true shows archived
    [Fact]
    public async Task ListCategories_IncludeArchived_ShowsArchivedCategory()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var cat = (await (await client.PostAsJsonAsync("/api/categories", new { name = "ArchivedCat" }))
            .Content.ReadFromJsonAsync<CategoryResponse>(JsonOptions))!;
        await client.PostAsync($"/api/categories/{cat.Id}/archive", null);

        var list = await (await client.GetAsync("/api/categories?includeArchived=true"))
            .Content.ReadFromJsonAsync<List<CategoryResponse>>(JsonOptions);

        Assert.Contains(list!, c => c.Id == cat.Id && c.IsArchived);
    }

    // Test T — Unarchive a category
    [Fact]
    public async Task UnarchiveCategory_Succeeds_AppearsInDefaultList()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var cat = (await (await client.PostAsJsonAsync("/api/categories", new { name = "ArchiveThenBack" }))
            .Content.ReadFromJsonAsync<CategoryResponse>(JsonOptions))!;
        await client.PostAsync($"/api/categories/{cat.Id}/archive", null);

        var unarchiveResponse = await client.PostAsync($"/api/categories/{cat.Id}/unarchive", null);
        Assert.Equal(HttpStatusCode.NoContent, unarchiveResponse.StatusCode);

        var list = await (await client.GetAsync("/api/categories"))
            .Content.ReadFromJsonAsync<List<CategoryResponse>>(JsonOptions);
        Assert.Contains(list!, c => c.Id == cat.Id && !c.IsArchived);
    }

    // Test U — Unarchive blocked by name conflict
    [Fact]
    public async Task UnarchiveCategory_NameConflict_Returns409()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var cat1 = (await (await client.PostAsJsonAsync("/api/categories", new { name = "Shared" }))
            .Content.ReadFromJsonAsync<CategoryResponse>(JsonOptions))!;
        await client.PostAsync($"/api/categories/{cat1.Id}/archive", null);

        // Create a second active category with the same name
        await client.PostAsJsonAsync("/api/categories", new { name = "Shared" });

        var response = await client.PostAsync($"/api/categories/{cat1.Id}/unarchive", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // Test V — Category isolation between users
    [Fact]
    public async Task Categories_AreIsolatedPerUser()
    {
        var (tokenA, _) = await RegisterUserAsync(UniqueEmail());
        var (tokenB, _) = await RegisterUserAsync(UniqueEmail());
        var clientA = CreateAuthenticatedClient(tokenA);
        var clientB = CreateAuthenticatedClient(tokenB);

        await clientA.PostAsJsonAsync("/api/categories", new { name = "UserAOnly" });

        var listB = await (await clientB.GetAsync("/api/categories"))
            .Content.ReadFromJsonAsync<List<CategoryResponse>>(JsonOptions);

        Assert.Empty(listB!);
    }

    // Test W — Rename 404 on unknown category id
    [Fact]
    public async Task RenameCategory_UnknownId_Returns404()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PutAsJsonAsync($"/api/categories/{Guid.NewGuid()}", new { name = "X" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Test W — Input validation: empty name
    [Fact]
    public async Task CreateCategory_EmptyName_Returns400()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/categories", new { name = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Unauthenticated access → 401
    [Fact]
    public async Task ListCategories_NoToken_Returns401()
    {
        var response = await Client.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record CategoryResponse(Guid Id, string Name, bool IsArchived);
}
