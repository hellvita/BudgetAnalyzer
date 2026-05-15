using BudgetAnalyzer.Application.Summaries;
using BudgetAnalyzer.Infrastructure.Export;
using ClosedXML.Excel;

namespace BudgetAnalyzer.UnitTests.Export;

public class ClosedXmlExportServiceTests
{
    private static readonly Guid CatId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string CatName = "Groceries";

    private static MonthSummaryResponse BuildSummary(
        decimal openingBalance,
        params (DateOnly Date, decimal Expense, decimal Income)[] days)
    {
        var dayItems = days.Select(d => new MonthSummaryDayItem(
            d.Date,
            d.Expense,
            d.Income,
            null, null,
            d.Income - d.Expense,
            new List<SummaryExpenseByCategory> { new(CatId, CatName, d.Expense) }
        )).ToList();

        var totalExpenses = days.Sum(d => d.Expense);
        var totalIncome = days.Sum(d => d.Income);

        var totals = new MonthTotals(
            totalExpenses,
            totalIncome,
            new List<SummaryExpenseByCategory> { new(CatId, CatName, totalExpenses) },
            0m, 0m,
            totalIncome - totalExpenses);

        return new MonthSummaryResponse(2026, 5, openingBalance, dayItems, totals);
    }

    private static (IXLWorkbook Wb, IXLWorksheet Ws) LoadXlsx(byte[] bytes)
    {
        var ms = new MemoryStream(bytes);
        var wb = new XLWorkbook(ms);
        return (wb, wb.Worksheets.First());
    }

    [Fact]
    public void RenderMonth_ProducesValidXlsx()
    {
        var summary = BuildSummary(1000m, (new DateOnly(2026, 5, 1), 15.70m, 0m));

        var bytes = new ClosedXmlExportService().RenderMonth(summary);

        Assert.NotEmpty(bytes);
        var (wb, _) = LoadXlsx(bytes);
        wb.Dispose();
    }

    [Fact]
    public void RenderMonth_RowCount_IsHeaderPlusDaysPlusTotals()
    {
        var summary = BuildSummary(1000m,
            (new DateOnly(2026, 5, 1), 10m, 0m),
            (new DateOnly(2026, 5, 2), 20m, 0m),
            (new DateOnly(2026, 5, 3), 30m, 0m));

        var bytes = new ClosedXmlExportService().RenderMonth(summary);
        var (wb, ws) = LoadXlsx(bytes);

        Assert.Equal(summary.Days.Count + 2, ws.LastRowUsed()!.RowNumber());
        wb.Dispose();
    }

    [Fact]
    public void RenderMonth_ColumnCount_IsDatePlusCatsPlusTotalIncomeNetBalance()
    {
        var summary = BuildSummary(1000m, (new DateOnly(2026, 5, 1), 15m, 0m));

        var bytes = new ClosedXmlExportService().RenderMonth(summary);
        var (wb, ws) = LoadXlsx(bytes);

        // date + categories.Count + Total Expenses + Income + Net + Balance
        var expectedCols = 1 + summary.MonthTotals.ExpensesByCategory.Count + 1 + 1 + 1 + 1;
        Assert.Equal(expectedCols, ws.LastColumnUsed()!.ColumnNumber());
        Assert.Equal("Balance", ws.Cell(1, expectedCols).GetString());
        wb.Dispose();
    }

    [Fact]
    public void RenderMonth_TotalsRow_MatchesMonthTotals()
    {
        var summary = BuildSummary(1000m,
            (new DateOnly(2026, 5, 1), 15.70m, 0m),
            (new DateOnly(2026, 5, 2), 8.30m, 0m));

        var bytes = new ClosedXmlExportService().RenderMonth(summary);
        var (wb, ws) = LoadXlsx(bytes);

        int totalsRow = summary.Days.Count + 2;
        int colTotal = 3; // date(1) + cat(1) + Total Expenses(1) = col 3
        Assert.Equal((double)summary.MonthTotals.TotalExpenses, ws.Cell(totalsRow, colTotal).GetDouble());
        wb.Dispose();
    }

    [Fact]
    public void RenderMonth_BalanceColumn_IncrementsCorrectly()
    {
        // openingBalance=1000, day1 expenses=15.70, day2 expenses=8.30
        var summary = BuildSummary(1000m,
            (new DateOnly(2026, 5, 1), 15.70m, 0m),
            (new DateOnly(2026, 5, 2), 8.30m, 0m));

        var bytes = new ClosedXmlExportService().RenderMonth(summary);
        var (wb, ws) = LoadXlsx(bytes);

        // With 1 category: colBalance = 1(date) + 1(cat) + 1(total) + 1(income) + 1(net) + 1(balance) = 6
        int colBalance = 6;
        Assert.Equal(984.30, ws.Cell(2, colBalance).GetDouble(), precision: 2);
        Assert.Equal(976.00, ws.Cell(3, colBalance).GetDouble(), precision: 2);
        wb.Dispose();
    }

    [Fact]
    public void RenderMonth_TotalsRowBalance_RepeatsLastDayBalance()
    {
        var summary = BuildSummary(1000m,
            (new DateOnly(2026, 5, 1), 15.70m, 0m),
            (new DateOnly(2026, 5, 2), 8.30m, 0m));

        var bytes = new ClosedXmlExportService().RenderMonth(summary);
        var (wb, ws) = LoadXlsx(bytes);

        int colBalance = 6;
        int lastDataRow = summary.Days.Count + 1;
        int totalsRow = summary.Days.Count + 2;

        Assert.Equal(
            ws.Cell(lastDataRow, colBalance).GetDouble(),
            ws.Cell(totalsRow, colBalance).GetDouble());
        wb.Dispose();
    }

    [Fact]
    public void RenderMonth_HeaderRow_IsBold()
    {
        var summary = BuildSummary(0m, (new DateOnly(2026, 5, 1), 10m, 0m));

        var bytes = new ClosedXmlExportService().RenderMonth(summary);
        var (wb, ws) = LoadXlsx(bytes);

        Assert.True(ws.Cell(1, 1).Style.Font.Bold);
        wb.Dispose();
    }

    // ── Limit columns ────────────────────────────────────────────────────────

    private static MonthSummaryResponse BuildSummaryWithLimit(
        decimal openingBalance,
        decimal dailyLimit,
        params (DateOnly Date, decimal Expense, decimal Income)[] days)
    {
        var dayItems = days.Select(d => new MonthSummaryDayItem(
            d.Date,
            d.Expense,
            d.Income,
            dailyLimit,
            dailyLimit - d.Expense,
            d.Income - d.Expense,
            new List<SummaryExpenseByCategory> { new(CatId, CatName, d.Expense) }
        )).ToList();

        var totalExpenses = days.Sum(d => d.Expense);
        var totalIncome   = days.Sum(d => d.Income);
        var allowedBudget = dailyLimit * days.Length;

        var totals = new MonthTotals(
            totalExpenses,
            totalIncome,
            new List<SummaryExpenseByCategory> { new(CatId, CatName, totalExpenses) },
            allowedBudget,
            allowedBudget - totalExpenses,
            totalIncome - totalExpenses);

        return new MonthSummaryResponse(2026, 5, openingBalance, dayItems, totals);
    }

    [Fact]
    public void RenderMonth_WithLimit_ColumnCount_IncludesTwoExtraColumns()
    {
        var summary = BuildSummaryWithLimit(1000m, 50m, (new DateOnly(2026, 5, 1), 15m, 0m));

        var bytes = new ClosedXmlExportService().RenderMonth(summary);
        var (wb, ws) = LoadXlsx(bytes);

        // date + cats + Total Expenses + Income + Net + Limit + Limit Diff + Balance
        var expectedCols = 1 + summary.MonthTotals.ExpensesByCategory.Count + 1 + 1 + 1 + 2 + 1;
        Assert.Equal(expectedCols, ws.LastColumnUsed()!.ColumnNumber());
        Assert.Equal("Limit",      ws.Cell(1, expectedCols - 2).GetString());
        Assert.Equal("Limit Diff", ws.Cell(1, expectedCols - 1).GetString());
        Assert.Equal("Balance",    ws.Cell(1, expectedCols).GetString());
        wb.Dispose();
    }

    [Fact]
    public void RenderMonth_WithLimit_DataRows_ShowLimitAndDiff()
    {
        var summary = BuildSummaryWithLimit(1000m, 50m, (new DateOnly(2026, 5, 1), 15m, 0m));

        var bytes = new ClosedXmlExportService().RenderMonth(summary);
        var (wb, ws) = LoadXlsx(bytes);

        // 1 cat → date(1)+cat(1)+total(1)+income(1)+net(1)+limit(1)+limitdiff(1)+balance(1)
        // colLimit = 6, colLimitDiff = 7
        Assert.Equal(50.0, ws.Cell(2, 6).GetDouble());
        Assert.Equal(35.0, ws.Cell(2, 7).GetDouble()); // 50 - 15
        wb.Dispose();
    }

    [Fact]
    public void RenderMonth_WithLimit_TotalsRow_ShowsAllowedBudgetAndTotalDiff()
    {
        var summary = BuildSummaryWithLimit(1000m, 50m,
            (new DateOnly(2026, 5, 1), 15m, 0m),
            (new DateOnly(2026, 5, 2), 20m, 0m));

        var bytes = new ClosedXmlExportService().RenderMonth(summary);
        var (wb, ws) = LoadXlsx(bytes);

        int totalsRow = summary.Days.Count + 2;
        // AllowedMonthlyBudget = 50*2 = 100, TotalLimitDiff = 100 - 35 = 65
        Assert.Equal(100.0, ws.Cell(totalsRow, 6).GetDouble());
        Assert.Equal(65.0,  ws.Cell(totalsRow, 7).GetDouble());
        wb.Dispose();
    }

    [Fact]
    public void RenderMonth_WithoutLimit_NoLimitColumns()
    {
        var summary = BuildSummary(1000m, (new DateOnly(2026, 5, 1), 15m, 0m));

        var bytes = new ClosedXmlExportService().RenderMonth(summary);
        var (wb, ws) = LoadXlsx(bytes);

        Assert.Equal("Balance", ws.Cell(1, ws.LastColumnUsed()!.ColumnNumber()).GetString());
        wb.Dispose();
    }
}
