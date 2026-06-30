using BudgetAnalyzer.Application.Import;
using BudgetAnalyzer.Application.Import.Dtos;
using BudgetAnalyzer.Domain.Exceptions;
using BudgetAnalyzer.Infrastructure.Import;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging.Abstractions;

namespace BudgetAnalyzer.UnitTests.Import;

public class ClosedXmlParserTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    private static ClosedXmlParser CreateParser() =>
        new(NullLogger<ClosedXmlParser>.Instance);

    private string CreateTempXlsx(Action<IXLWorkbook> configure)
    {
        using var wb = new XLWorkbook();
        configure(wb);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        wb.SaveAs(path);
        _tempFiles.Add(path);
        return path;
    }

    private string CreateSingleSheetXlsx(Action<IXLWorksheet> configure)
        => CreateTempXlsx(wb => configure(wb.Worksheets.Add("Sheet1")));

    public void Dispose()
    {
        foreach (var path in _tempFiles)
            if (File.Exists(path))
                File.Delete(path);
    }

    private static ColumnMappingDto MakeMapping(
        int dateCol, int[] catCols, int incomeCol,
        decimal scale = 1m, bool invert = false)
        => new("fileid", dateCol, catCols.ToList(), incomeCol, scale, invert);

    [Fact]
    public void DetectColumns_SingleSheetWith3DataRows_ReturnsAllNonEmptyColumns()
    {
        var path = CreateSingleSheetXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Groceries";
            ws.Cell(1, 3).Value = "Income";
            for (int row = 2; row <= 4; row++)
            {
                ws.Cell(row, 1).Value = $"2025-0{row - 1}-01";
                ws.Cell(row, 2).Value = (double)(row * 10);
                ws.Cell(row, 3).Value = (double)(row * 100);
            }
        });

        var columns = CreateParser().DetectColumns(path);

        Assert.Equal(3, columns.Count);
        Assert.Equal("Date", columns[0].Header);
        Assert.Equal("Groceries", columns[1].Header);
        Assert.Equal("Income", columns[2].Header);
    }

    [Fact]
    public void DetectColumns_MultiSheetFile_ThrowsValidationException()
    {
        var path = CreateTempXlsx(wb =>
        {
            wb.Worksheets.Add("Sheet1").Cell(1, 1).Value = "Date";
            wb.Worksheets.Add("Sheet2").Cell(1, 1).Value = "Date";
        });

        Assert.Throws<ValidationException>(() => CreateParser().DetectColumns(path));
    }

    [Fact]
    public void DetectColumns_EmptyColumn_IsExcluded()
    {
        var path = CreateSingleSheetXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "EmptyCol";
            ws.Cell(1, 3).Value = "Groceries";
            ws.Cell(2, 1).Value = "2025-01-01";
            // column B (index 1) intentionally left empty in all data rows
            ws.Cell(2, 3).Value = 50.0;
        });

        var columns = CreateParser().DetectColumns(path);

        Assert.Equal(2, columns.Count);
        Assert.DoesNotContain(columns, c => c.Header == "EmptyCol");
    }

    [Fact]
    public void ReadRows_OaDateCell_ParsesCorrectly()
    {
        // OA date 45788.0 corresponds to 2025-05-11
        var path = CreateSingleSheetXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Expense";
            ws.Cell(1, 3).Value = "Income";
            ws.Cell(2, 1).Value = 45788.0;
            ws.Cell(2, 2).Value = 100.0;
            ws.Cell(2, 3).Value = 0.0;
        });

        var (rows, skipped) = CreateParser().ReadRows(path, MakeMapping(0, [1], 2));

        Assert.Single(rows);
        Assert.Equal(new DateOnly(2025, 5, 11), rows[0].Date);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void ReadRows_StringDateCell_ParsesCorrectly()
    {
        var path = CreateSingleSheetXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Expense";
            ws.Cell(1, 3).Value = "Income";
            ws.Cell(2, 1).Value = "2025-05-11";
            ws.Cell(2, 2).Value = 100.0;
            ws.Cell(2, 3).Value = 0.0;
        });

        var (rows, skipped) = CreateParser().ReadRows(path, MakeMapping(0, [1], 2));

        Assert.Single(rows);
        Assert.Equal(new DateOnly(2025, 5, 11), rows[0].Date);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void ReadRows_PartialYearMonthDate_IsSkippedNotCoercedToFirstOfMonth()
    {
        // A "2025-05" month-grouping row sits above the genuine "2025-05-01"
        // daily row. A lenient parse would coerce "2025-05" into 2025-05-01,
        // colliding with the real first-of-month row (duplicate dates). The
        // month row must be skipped instead.
        var path = CreateSingleSheetXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Expense";
            ws.Cell(1, 3).Value = "Income";
            ws.Cell(2, 1).Value = "2025-05";      // month-grouping row
            ws.Cell(2, 2).Value = 999.0;
            ws.Cell(2, 3).Value = 0.0;
            ws.Cell(3, 1).Value = "2025-05-01";   // genuine daily row
            ws.Cell(3, 2).Value = 100.0;
            ws.Cell(3, 3).Value = 0.0;
        });

        var (rows, skipped) = CreateParser().ReadRows(path, MakeMapping(0, [1], 2));

        Assert.Single(rows);
        Assert.Equal(new DateOnly(2025, 5, 1), rows[0].Date);
        Assert.Equal(100m, rows[0].CategoryAmounts[1]);
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void ReadRows_NonNumericExpenseCell_SkipsRow()
    {
        var path = CreateSingleSheetXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Expense";
            ws.Cell(1, 3).Value = "Income";
            ws.Cell(2, 1).Value = "2025-05-11";
            ws.Cell(2, 2).Value = "not-a-number";
            ws.Cell(2, 3).Value = 0.0;
            ws.Cell(3, 1).Value = "2025-05-12";
            ws.Cell(3, 2).Value = 50.0;
            ws.Cell(3, 3).Value = 0.0;
        });

        var (rows, skipped) = CreateParser().ReadRows(path, MakeMapping(0, [1], 2));

        Assert.Single(rows);
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void ReadRows_ScaleFactor_IsAppliedToExpenseAmount()
    {
        var path = CreateSingleSheetXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Expense";
            ws.Cell(1, 3).Value = "Income";
            ws.Cell(2, 1).Value = "2025-05-11";
            ws.Cell(2, 2).Value = 0.35;
            ws.Cell(2, 3).Value = 0.0;
        });

        var (rows, _) = CreateParser().ReadRows(path, MakeMapping(0, [1], 2, scale: 1000m));

        Assert.Single(rows);
        Assert.Equal(350m, rows[0].CategoryAmounts[1]);
    }

    [Fact]
    public void ReadRows_InvertSign_NegatesExpenseAmount()
    {
        var path = CreateSingleSheetXlsx(ws =>
        {
            ws.Cell(1, 1).Value = "Date";
            ws.Cell(1, 2).Value = "Expense";
            ws.Cell(1, 3).Value = "Income";
            ws.Cell(2, 1).Value = "2025-05-11";
            ws.Cell(2, 2).Value = 100.0;
            ws.Cell(2, 3).Value = 0.0;
        });

        var (rows, _) = CreateParser().ReadRows(path, MakeMapping(0, [1], 2, scale: 1m, invert: true));

        Assert.Single(rows);
        Assert.Equal(-100m, rows[0].CategoryAmounts[1]);
    }
}
