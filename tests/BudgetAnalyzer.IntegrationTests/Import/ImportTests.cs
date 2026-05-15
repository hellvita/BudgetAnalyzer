using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BudgetAnalyzer.IntegrationTests.Infrastructure;
using ClosedXML.Excel;

namespace BudgetAnalyzer.IntegrationTests.Import;

[Collection("Integration")]
public class ImportTests : IntegrationTestBase
{
    private static string UniqueEmail() => $"import-{Guid.NewGuid():N}@tests.budget.dev";

    public ImportTests(BudgetApiFactory factory) : base(factory) { }

    private static byte[] BuildSingleSheetXlsx(Action<IXLWorksheet> configure)
    {
        using var wb = new XLWorkbook();
        configure(wb.Worksheets.Add("Sheet1"));
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] BuildMultiSheetXlsx()
    {
        using var wb = new XLWorkbook();
        wb.Worksheets.Add("Sheet1").Cell(1, 1).Value = "Date";
        wb.Worksheets.Add("Sheet2").Cell(1, 1).Value = "Date";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static MultipartFormDataContent BuildFileContent(byte[] xlsxBytes, string fileName = "test.xlsx")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(xlsxBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    // Test I1 — Full import flow: parse → preview → execute → verify via day summary
    [Fact]
    public async Task FullImportFlow_ParsePreviewExecute_PersistsData()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var date1 = new DateOnly(2026, 3, 1);
        var date2 = new DateOnly(2026, 3, 2);
        var date3 = new DateOnly(2026, 3, 3);

        var xlsxBytes = BuildSingleSheetXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Groceries";
            ws.Cell(1, 3).Value = "Transport";
            ws.Cell(1, 4).Value = "Income";
            ws.Cell(2, 1).Value = date1.ToString("yyyy-MM-dd");
            ws.Cell(2, 2).Value = 50.0;
            ws.Cell(2, 3).Value = 20.0;
            ws.Cell(2, 4).Value = 0.0;
            ws.Cell(3, 1).Value = date2.ToString("yyyy-MM-dd");
            ws.Cell(3, 2).Value = 30.0;
            ws.Cell(3, 3).Value = 15.0;
            ws.Cell(3, 4).Value = 0.0;
            ws.Cell(4, 1).Value = date3.ToString("yyyy-MM-dd");
            ws.Cell(4, 2).Value = 25.0;
            ws.Cell(4, 3).Value = 10.0;
            ws.Cell(4, 4).Value = 1000.0;
        });

        // Step 1: Parse
        var parseResp = await client.PostAsync("/api/import/parse", BuildFileContent(xlsxBytes));
        Assert.Equal(HttpStatusCode.OK, parseResp.StatusCode);
        var parsed = await parseResp.Content.ReadFromJsonAsync<ParseResult>(JsonOptions);
        Assert.NotNull(parsed);
        Assert.Equal(4, parsed.Columns.Count);

        // Step 2: Preview
        var previewResp = await client.PostAsJsonAsync("/api/import/preview", new
        {
            fileId = parsed.FileId,
            dateColumnIndex = 0,
            categoryColumnIndexes = new[] { 1, 2 },
            incomeColumnIndex = 3,
            scaleFactor = 1m,
            invertSign = false
        });
        Assert.Equal(HttpStatusCode.OK, previewResp.StatusCode);
        var preview = await previewResp.Content.ReadFromJsonAsync<PreviewResult>(JsonOptions);
        Assert.NotNull(preview);
        Assert.Equal(3, preview.TotalDataRows);
        Assert.Equal(3, preview.Preview.Count);

        // Step 3: Execute
        var executeResp = await client.PostAsJsonAsync("/api/import/execute", new
        {
            fileId = parsed.FileId,
            dateColumnIndex = 0,
            categoryColumnIndexes = new[] { 1, 2 },
            incomeColumnIndex = 3,
            scaleFactor = 1m,
            invertSign = false
        });
        Assert.Equal(HttpStatusCode.OK, executeResp.StatusCode);
        var imported = await executeResp.Content.ReadFromJsonAsync<ImportResult>(JsonOptions);
        Assert.NotNull(imported);
        Assert.Equal(3, imported.DaysImported);
        Assert.Contains("Groceries", imported.CategoriesCreated);
        Assert.Contains("Transport", imported.CategoriesCreated);

        // Step 4: Verify via day summary
        var dayResp = await client.GetAsync($"/api/summary/day/{date1:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, dayResp.StatusCode);
        var day = await dayResp.Content.ReadFromJsonAsync<DaySummary>(JsonOptions);
        Assert.NotNull(day);
        var groceries = day.ExpensesByCategory.SingleOrDefault(e => e.CategoryName == "Groceries");
        var transport = day.ExpensesByCategory.SingleOrDefault(e => e.CategoryName == "Transport");
        Assert.NotNull(groceries);
        Assert.Equal(50m, groceries.Amount);
        Assert.NotNull(transport);
        Assert.Equal(20m, transport.Amount);
    }

    // Test I2 — Multi-sheet file is rejected with 400
    [Fact]
    public async Task Parse_MultiSheetXlsx_Returns400WithMessage()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsync("/api/import/parse", BuildFileContent(BuildMultiSheetXlsx()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("exactly one sheet", body, StringComparison.OrdinalIgnoreCase);
    }

    // Test I3 — Preview with unknown fileId returns 404
    [Fact]
    public async Task Preview_UnknownFileId_Returns404()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var response = await client.PostAsJsonAsync("/api/import/preview", new
        {
            fileId = Guid.NewGuid().ToString("N"),
            dateColumnIndex = 0,
            categoryColumnIndexes = new[] { 1 },
            incomeColumnIndex = 2,
            scaleFactor = 1m,
            invertSign = false
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Test I4 — Scale factor applied end-to-end
    [Fact]
    public async Task Execute_WithScaleFactor_StoresTransformedAmount()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var date = new DateOnly(2026, 4, 10);
        var xlsxBytes = BuildSingleSheetXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Savings";
            ws.Cell(1, 3).Value = "Income";
            ws.Cell(2, 1).Value = date.ToString("yyyy-MM-dd");
            ws.Cell(2, 2).Value = 0.35;
            ws.Cell(2, 3).Value = 0.0;
        });

        var parseResp = await client.PostAsync("/api/import/parse", BuildFileContent(xlsxBytes));
        var parsed = await parseResp.Content.ReadFromJsonAsync<ParseResult>(JsonOptions);
        Assert.NotNull(parsed);

        var executeResp = await client.PostAsJsonAsync("/api/import/execute", new
        {
            fileId = parsed.FileId,
            dateColumnIndex = 0,
            categoryColumnIndexes = new[] { 1 },
            incomeColumnIndex = 2,
            scaleFactor = 1000m,
            invertSign = false
        });
        Assert.Equal(HttpStatusCode.OK, executeResp.StatusCode);

        var dayResp = await client.GetAsync($"/api/summary/day/{date:yyyy-MM-dd}");
        var day = await dayResp.Content.ReadFromJsonAsync<DaySummary>(JsonOptions);
        Assert.NotNull(day);
        var savings = day.ExpensesByCategory.Single(e => e.CategoryName == "Savings");
        Assert.Equal(350m, savings.Amount);
    }

    // Test I5 — Export returns valid xlsx with seeded data
    [Fact]
    public async Task Export_Month_ReturnsXlsxWithCorrectExpenseRow()
    {
        var (token, _) = await RegisterUserAsync(UniqueEmail());
        var client = CreateAuthenticatedClient(token);

        var catResp = await client.PostAsJsonAsync("/api/categories", new { name = "Rent" });
        catResp.EnsureSuccessStatusCode();
        var category = await catResp.Content.ReadFromJsonAsync<CategoryResponse>(JsonOptions);
        Assert.NotNull(category);

        var date = new DateOnly(2026, 6, 15);
        var upsertResp = await client.PutAsJsonAsync(
            $"/api/expenses/{date:yyyy-MM-dd}/{category.Id}", new { amount = 1500m });
        upsertResp.EnsureSuccessStatusCode();

        var exportResp = await client.GetAsync("/api/export/month/2026-06");

        Assert.Equal(HttpStatusCode.OK, exportResp.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            exportResp.Content.Headers.ContentType?.MediaType);

        var contentDisposition = exportResp.Content.Headers.ContentDisposition;
        var fileName = contentDisposition?.FileNameStar ?? contentDisposition?.FileName ?? string.Empty;
        Assert.Contains("budget-2026-06", fileName.Trim('"'), StringComparison.OrdinalIgnoreCase);

        var bytes = await exportResp.Content.ReadAsByteArrayAsync();
        using var ms = new MemoryStream(bytes);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheets.First();

        int dataRow = -1;
        for (int r = 2; r <= ws.LastRowUsed()!.RowNumber() - 1; r++)
        {
            if (ws.Cell(r, 1).GetString() == date.ToString("yyyy-MM-dd"))
            {
                dataRow = r;
                break;
            }
        }

        Assert.NotEqual(-1, dataRow);
        Assert.Equal(1500.0, ws.Cell(dataRow, 2).GetDouble());
    }

    // Test I6 — All import/export endpoints require authentication
    [Fact]
    public async Task AllEndpoints_NoToken_Return401()
    {
        var parseResp = await Client.PostAsync("/api/import/parse",
            BuildFileContent(BuildSingleSheetXlsx(ws => ws.Cell(1, 1).Value = "x")));
        Assert.Equal(HttpStatusCode.Unauthorized, parseResp.StatusCode);

        var previewResp = await Client.PostAsJsonAsync("/api/import/preview",
            new { fileId = "x", dateColumnIndex = 0, categoryColumnIndexes = new[] { 1 }, incomeColumnIndex = 2, scaleFactor = 1m, invertSign = false });
        Assert.Equal(HttpStatusCode.Unauthorized, previewResp.StatusCode);

        var executeResp = await Client.PostAsJsonAsync("/api/import/execute",
            new { fileId = "x", dateColumnIndex = 0, categoryColumnIndexes = new[] { 1 }, incomeColumnIndex = 2, scaleFactor = 1m, invertSign = false });
        Assert.Equal(HttpStatusCode.Unauthorized, executeResp.StatusCode);

        var exportResp = await Client.GetAsync("/api/export/month/2026-05");
        Assert.Equal(HttpStatusCode.Unauthorized, exportResp.StatusCode);
    }

    private record ParsedColumn(int Index, string Letter, string Header);
    private record ParseResult(string FileId, List<ParsedColumn> Columns);
    private record PreviewExpense(string CategoryName, decimal Amount);
    private record PreviewRow(DateOnly Date, List<PreviewExpense> Expenses, decimal Income);
    private record PreviewResult(int TotalDataRows, int SkippedRows, List<PreviewRow> Preview);
    private record ImportResult(int DaysImported, int RowsSkipped, List<string> CategoriesCreated, int ExpensesUpserted, int IncomesUpserted);
    private record ExpenseByCategory(Guid CategoryId, string CategoryName, decimal Amount);
    private record DaySummary(DateOnly Date, decimal Income, List<ExpenseByCategory> ExpensesByCategory, decimal TotalExpenses);
    private record CategoryResponse(Guid Id, string Name, bool IsArchived);
}
