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

    // ── RenderAllMonthsCombined ────────────────────────────────────────────────

    private static readonly Guid CatId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string CatName2 = "Transport";

    private static MonthSummaryResponse BuildSummaryForMonth(
        int year, int month, decimal openingBalance,
        params (DateOnly Date, decimal Expense, decimal Income)[] days)
    {
        var dayItems = days.Select(d => new MonthSummaryDayItem(
            d.Date, d.Expense, d.Income, null, null,
            d.Income - d.Expense,
            new List<SummaryExpenseByCategory> { new(CatId, CatName, d.Expense) }
        )).ToList();

        var totalExpenses = days.Sum(d => d.Expense);
        var totalIncome = days.Sum(d => d.Income);

        var totals = new MonthTotals(
            totalExpenses, totalIncome,
            new List<SummaryExpenseByCategory> { new(CatId, CatName, totalExpenses) },
            0m, 0m, totalIncome - totalExpenses);

        return new MonthSummaryResponse(year, month, openingBalance, dayItems, totals);
    }

    [Fact]
    public void RenderAllMonthsCombined_EmptySummaries_ReturnsValidXlsx()
    {
        var bytes = new ClosedXmlExportService().RenderAllMonthsCombined([]);

        Assert.NotEmpty(bytes);
        var (wb, ws) = LoadXlsx(bytes);
        Assert.Equal("All Time", ws.Name);
        wb.Dispose();
    }

    [Fact]
    public void RenderAllMonthsCombined_EmptySummaries_WritesHeaderRowWithRequiredColumns()
    {
        var bytes = new ClosedXmlExportService().RenderAllMonthsCombined([]);
        var (wb, ws) = LoadXlsx(bytes);

        Assert.Equal("Date",    ws.Cell(1, 1).GetString());
        Assert.Equal("Balance", ws.Cell(1, ws.LastColumnUsed()!.ColumnNumber()).GetString());
        wb.Dispose();
    }

    [Fact]
    public void RenderAllMonthsCombined_SingleMonth_RowCountIsCorrect()
    {
        // Rows: 1 header + 1 month-header + 2 day rows + 1 monthly total + 1 all-time = 6
        var summary = BuildSummaryForMonth(2026, 5, 1000m,
            (new DateOnly(2026, 5, 1), 10m, 0m),
            (new DateOnly(2026, 5, 2), 20m, 0m));

        var bytes = new ClosedXmlExportService().RenderAllMonthsCombined([summary]);
        var (wb, ws) = LoadXlsx(bytes);

        int expected = 1 + 1 + summary.Days.Count + 1 + 1;
        Assert.Equal(expected, ws.LastRowUsed()!.RowNumber());
        wb.Dispose();
    }

    [Fact]
    public void RenderAllMonthsCombined_MonthHeaderRow_HasCorrectLabelAndStyle()
    {
        var summary = BuildSummaryForMonth(2026, 5, 1000m,
            (new DateOnly(2026, 5, 1), 10m, 0m));

        var bytes = new ClosedXmlExportService().RenderAllMonthsCombined([summary]);
        var (wb, ws) = LoadXlsx(bytes);

        var cell = ws.Cell(2, 1); // month header is always row 2 for the first month
        Assert.Equal("2026-05", cell.GetString());
        Assert.True(cell.Style.Font.Bold);
        Assert.Equal(XLColor.LightSteelBlue, cell.Style.Fill.BackgroundColor);
        wb.Dispose();
    }

    [Fact]
    public void RenderAllMonthsCombined_AllTimeRow_HasCorrectLabelAndStyle()
    {
        var summary = BuildSummaryForMonth(2026, 5, 1000m,
            (new DateOnly(2026, 5, 1), 10m, 0m));

        var bytes = new ClosedXmlExportService().RenderAllMonthsCombined([summary]);
        var (wb, ws) = LoadXlsx(bytes);

        // 1 header + 1 month-header + 1 day + 1 total + 1 all-time = 5
        var cell = ws.Cell(5, 1);
        Assert.Equal("All Time", cell.GetString());
        Assert.True(cell.Style.Font.Bold);
        Assert.Equal(XLColor.LightYellow, cell.Style.Fill.BackgroundColor);
        wb.Dispose();
    }

    [Fact]
    public void RenderAllMonthsCombined_AllTimeRow_SumsAcrossTwoMonths()
    {
        // Month April: 1 day, expense=30, income=0
        // Month May:   1 day, expense=50, income=200
        var m1 = BuildSummaryForMonth(2026, 4, 1000m, (new DateOnly(2026, 4, 1), 30m, 0m));
        var m2 = BuildSummaryForMonth(2026, 5, 970m,  (new DateOnly(2026, 5, 1), 50m, 200m));

        var bytes = new ClosedXmlExportService().RenderAllMonthsCombined([m1, m2]);
        var (wb, ws) = LoadXlsx(bytes);

        // Rows: 1(header) + 1(Apr-header) + 1(Apr-day) + 1(Apr-total)
        //     + 1(May-header) + 1(May-day) + 1(May-total) + 1(all-time) = 8
        int allTimeRow = 8;
        // col layout with 1 cat, no limit: date(1)+Groceries(2)+TotalExp(3)+Income(4)+Net(5)+Balance(6)
        Assert.Equal("All Time", ws.Cell(allTimeRow, 1).GetString());
        Assert.Equal(80.0,  ws.Cell(allTimeRow, 3).GetDouble()); // 30 + 50
        Assert.Equal(200.0, ws.Cell(allTimeRow, 4).GetDouble()); // 0 + 200
        Assert.Equal(120.0, ws.Cell(allTimeRow, 5).GetDouble()); // 200 - 80
        wb.Dispose();
    }

    [Fact]
    public void RenderAllMonthsCombined_Balance_IsContinuousAcrossMonths()
    {
        // opening=1000, m1: expense=30 → balance=970 at end of m1
        // m2 opening balance in the summary object (500m) is ignored — balance runs continuously
        var m1 = BuildSummaryForMonth(2026, 4, 1000m, (new DateOnly(2026, 4, 1), 30m, 0m));
        var m2 = BuildSummaryForMonth(2026, 5, 500m,  (new DateOnly(2026, 5, 1), 50m, 0m));

        var bytes = new ClosedXmlExportService().RenderAllMonthsCombined([m1, m2]);
        var (wb, ws) = LoadXlsx(bytes);

        int colBalance = 6; // date+cat+total+income+net+balance
        // Row 3: m1 day 1 → 1000 - 30 = 970
        Assert.Equal(970.0, ws.Cell(3, colBalance).GetDouble());
        // Row 6: m2 day 1 → 970 - 50 = 920  (continuous, not reset to m2.OpeningBalance)
        Assert.Equal(920.0, ws.Cell(6, colBalance).GetDouble());
        wb.Dispose();
    }

    [Fact]
    public void RenderAllMonthsCombined_CategoryUnion_AllCategoriesAppearAsColumns()
    {
        // Month 1: only Groceries. Month 2: only Transport. Union → both columns present.
        var dayItemsM1 = new List<MonthSummaryDayItem>
        {
            new(new DateOnly(2026, 4, 1), 30m, 0m, null, null, -30m,
                [new(CatId, CatName, 30m)])
        };
        var totalsM1 = new MonthTotals(30m, 0m,
            [new(CatId, CatName, 30m)], 0m, 0m, -30m);
        var m1 = new MonthSummaryResponse(2026, 4, 1000m, dayItemsM1, totalsM1);

        var dayItemsM2 = new List<MonthSummaryDayItem>
        {
            new(new DateOnly(2026, 5, 1), 50m, 0m, null, null, -50m,
                [new(CatId2, CatName2, 50m)])
        };
        var totalsM2 = new MonthTotals(50m, 0m,
            [new(CatId2, CatName2, 50m)], 0m, 0m, -50m);
        var m2 = new MonthSummaryResponse(2026, 5, 970m, dayItemsM2, totalsM2);

        var bytes = new ClosedXmlExportService().RenderAllMonthsCombined([m1, m2]);
        var (wb, ws) = LoadXlsx(bytes);

        // With 2 categories the column headers should include both names
        var headerValues = Enumerable.Range(1, ws.LastColumnUsed()!.ColumnNumber())
            .Select(c => ws.Cell(1, c).GetString())
            .ToList();

        Assert.Contains(CatName,  headerValues);
        Assert.Contains(CatName2, headerValues);
        wb.Dispose();
    }

    [Fact]
    public void RenderAllMonthsCombined_TwoMonths_ColumnCount_MatchesUnionOfCategories()
    {
        var m1 = BuildSummaryForMonth(2026, 4, 1000m, (new DateOnly(2026, 4, 1), 30m, 0m));
        var m2 = BuildSummaryForMonth(2026, 5, 970m,  (new DateOnly(2026, 5, 1), 50m, 0m));

        // Both months use the same single category (CatId/Groceries)
        var bytes = new ClosedXmlExportService().RenderAllMonthsCombined([m1, m2]);
        var (wb, ws) = LoadXlsx(bytes);

        // date + 1 cat + Total Expenses + Income + Net + Balance = 6
        int expectedCols = 1 + 1 + 1 + 1 + 1 + 1;
        Assert.Equal(expectedCols, ws.LastColumnUsed()!.ColumnNumber());
        wb.Dispose();
    }
}
