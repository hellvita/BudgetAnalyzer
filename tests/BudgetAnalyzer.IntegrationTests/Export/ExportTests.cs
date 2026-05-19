using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using BudgetAnalyzer.IntegrationTests.Infrastructure;
using ClosedXML.Excel;

namespace BudgetAnalyzer.IntegrationTests.Export;

[Collection("Integration")]
public class ExportTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"export-{Guid.NewGuid():N}@tests.budget.dev";

    public ExportTests(BudgetApiFactory factory) : base(factory) { }

    private static async Task<CategoryDto> CreateCategoryAsync(HttpClient client, string name)
    {
        var resp = await client.PostAsJsonAsync("/api/categories", new { name });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions))!;
    }

    // ── GET /api/export/zip ────────────────────────────────────────────────────

    [Fact]
    public async Task ExportZip_NoToken_Returns401()
    {
        var resp = await Client.GetAsync("/api/export/zip");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ExportZip_NoData_Returns204()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var resp = await client.GetAsync("/api/export/zip");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task ExportZip_WithData_Returns200WithZipContentType()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat = await CreateCategoryAsync(client, "Groceries");
        await client.PutAsJsonAsync("/api/expenses/2026-03-15/" + cat.Id, new { amount = 25.50m });

        var resp = await client.GetAsync("/api/export/zip");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("application/zip", resp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ExportZip_WithData_ArchiveContainsOneFilePerMonth()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat = await CreateCategoryAsync(client, "Food");

        // Expenses in March and May → expect 2 files in the archive
        await client.PutAsJsonAsync("/api/expenses/2026-03-10/" + cat.Id, new { amount = 40m });
        await client.PutAsJsonAsync("/api/expenses/2026-05-20/" + cat.Id, new { amount = 60m });

        var resp = await client.GetAsync("/api/export/zip");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var bytes = await resp.Content.ReadAsByteArrayAsync();
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

        var names = archive.Entries.Select(e => e.FullName).OrderBy(n => n).ToList();
        Assert.Equal(2, names.Count);
        Assert.Equal("budget-2026-03.xlsx", names[0]);
        Assert.Equal("budget-2026-05.xlsx", names[1]);
    }

    [Fact]
    public async Task ExportZip_WithData_EachEntryIsValidXlsx()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat = await CreateCategoryAsync(client, "Rent");
        await client.PutAsJsonAsync("/api/expenses/2026-04-01/" + cat.Id, new { amount = 800m });

        var resp = await client.GetAsync("/api/export/zip");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

        var entry = archive.Entries.Single();
        using var entryStream = entry.Open();
        using var ms = new MemoryStream();
        await entryStream.CopyToAsync(ms);
        ms.Position = 0;

        using var wb = new XLWorkbook(ms);
        Assert.Single(wb.Worksheets);
        Assert.Equal("2026-04", wb.Worksheets.First().Name);
    }

    // ── GET /api/export/combined ───────────────────────────────────────────────

    [Fact]
    public async Task ExportCombined_NoToken_Returns401()
    {
        var resp = await Client.GetAsync("/api/export/combined");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ExportCombined_NoData_Returns200WithXlsxContentType()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var resp = await client.GetAsync("/api/export/combined");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            resp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ExportCombined_NoData_ReturnsXlsxWithAllTimeSheet()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var bytes = await (await client.GetAsync("/api/export/combined")).Content.ReadAsByteArrayAsync();
        using var wb = new XLWorkbook(new MemoryStream(bytes));

        Assert.Single(wb.Worksheets);
        Assert.Equal("All Time", wb.Worksheets.First().Name);
    }

    [Fact]
    public async Task ExportCombined_WithData_Returns200WithXlsxContentType()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat = await CreateCategoryAsync(client, "Transport");
        await client.PutAsJsonAsync("/api/expenses/2026-06-10/" + cat.Id, new { amount = 15m });

        var resp = await client.GetAsync("/api/export/combined");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            resp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ExportCombined_WithData_SheetIsNamedAllTime()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat = await CreateCategoryAsync(client, "Bills");
        await client.PutAsJsonAsync("/api/expenses/2026-07-05/" + cat.Id, new { amount = 120m });

        var bytes = await (await client.GetAsync("/api/export/combined")).Content.ReadAsByteArrayAsync();
        using var wb = new XLWorkbook(new MemoryStream(bytes));

        Assert.Equal("All Time", wb.Worksheets.First().Name);
    }

    [Fact]
    public async Task ExportCombined_WithData_ContainsMonthHeaderAndAllTimeRow()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);
        var cat = await CreateCategoryAsync(client, "Groceries");
        await client.PutAsJsonAsync("/api/expenses/2026-09-01/" + cat.Id, new { amount = 45m });

        var bytes = await (await client.GetAsync("/api/export/combined")).Content.ReadAsByteArrayAsync();
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.First();

        // Row 1: header. Row 2: month header "2026-09". Last row: "All Time".
        Assert.Equal("2026-09", ws.Cell(2, 1).GetString());
        Assert.Equal("All Time", ws.LastRowUsed()!.Cell(1).GetString());
    }

    private record CategoryDto(Guid Id, string Name, bool IsArchived);
}
