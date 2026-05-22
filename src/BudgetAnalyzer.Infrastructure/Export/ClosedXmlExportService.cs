using BudgetAnalyzer.Application.Export;
using BudgetAnalyzer.Application.Summaries;
using ClosedXML.Excel;

namespace BudgetAnalyzer.Infrastructure.Export;

public class ClosedXmlExportService : IExportRenderer
{
    public byte[] RenderMonth(MonthSummaryResponse summary)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add($"{summary.Year}-{summary.Month:D2}");

        var cats = summary.MonthTotals.ExpensesByCategory;
        bool hasLimit = summary.Days.Any(d => d.EffectiveLimit.HasValue);

        // Column positions (1-based)
        int colDate = 1;
        int colCatBase = 2;
        int colTotal = colCatBase + cats.Count;
        int colIncome = colTotal + 1;
        int colNet = colIncome + 1;
        int colLimit = hasLimit ? colNet + 1 : 0;
        int colLimitDiff = hasLimit ? colNet + 2 : 0;
        int colBalance = hasLimit ? colNet + 3 : colNet + 1;
        int lastCol = colBalance;

        // Header row
        ws.Cell(1, colDate).Value = "Date";
        for (int i = 0; i < cats.Count; i++)
            ws.Cell(1, colCatBase + i).Value = cats[i].CategoryName;
        ws.Cell(1, colTotal).Value = "Total Expenses";
        ws.Cell(1, colIncome).Value = "Income";
        ws.Cell(1, colNet).Value = "Net";
        if (hasLimit)
        {
            ws.Cell(1, colLimit).Value = "Limit";
            ws.Cell(1, colLimitDiff).Value = "Limit Diff";
        }
        ws.Cell(1, colBalance).Value = "Balance";

        var headerRange = ws.Range(1, 1, 1, lastCol);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Data rows with running balance
        decimal runningBalance = summary.OpeningBalance;

        for (int r = 0; r < summary.Days.Count; r++)
        {
            var day = summary.Days[r];
            int row = r + 2;

            runningBalance += day.TotalIncome - day.TotalExpenses;

            ws.Cell(row, colDate).Value = day.Date.ToString("yyyy-MM-dd");

            for (int i = 0; i < cats.Count; i++)
            {
                var match = day.ExpensesByCategory
                    .FirstOrDefault(e => e.CategoryId == cats[i].CategoryId);
                ws.Cell(row, colCatBase + i).Value = (double)(match?.Amount ?? 0m);
            }

            ws.Cell(row, colTotal).Value = (double)day.TotalExpenses;
            ws.Cell(row, colIncome).Value = (double)day.TotalIncome;
            ws.Cell(row, colNet).Value = (double)day.Net;
            if (hasLimit)
            {
                if (day.EffectiveLimit.HasValue)
                    ws.Cell(row, colLimit).Value = (double)day.EffectiveLimit.Value;
                if (day.LimitDiff.HasValue)
                    ws.Cell(row, colLimitDiff).Value = (double)day.LimitDiff.Value;
            }
            ws.Cell(row, colBalance).Value = (double)runningBalance;
        }

        // Totals row
        int totalsRow = summary.Days.Count + 2;
        ws.Cell(totalsRow, colDate).Value = "Total";

        for (int i = 0; i < cats.Count; i++)
            ws.Cell(totalsRow, colCatBase + i).Value = (double)cats[i].Amount;

        ws.Cell(totalsRow, colTotal).Value = (double)summary.MonthTotals.TotalExpenses;
        ws.Cell(totalsRow, colIncome).Value = (double)summary.MonthTotals.TotalIncome;
        ws.Cell(totalsRow, colNet).Value = (double)summary.MonthTotals.Net;
        if (hasLimit)
        {
            ws.Cell(totalsRow, colLimit).Value = (double)summary.MonthTotals.AllowedMonthlyBudget;
            ws.Cell(totalsRow, colLimitDiff).Value = (double)summary.MonthTotals.TotalLimitDiff;
        }
        ws.Cell(totalsRow, colBalance).Value = (double)runningBalance;

        var totalsRange = ws.Range(totalsRow, 1, totalsRow, lastCol);
        totalsRange.Style.Font.Bold = true;

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] RenderAllMonthsCombined(IReadOnlyList<MonthSummaryResponse> summaries)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("All Time");

        var allCats = summaries
            .SelectMany(s => s.MonthTotals.ExpensesByCategory)
            .GroupBy(c => c.CategoryId)
            .Select(g => g.First())
            .OrderBy(c => c.CategoryName)
            .ToList();

        bool hasLimit = summaries
            .SelectMany(s => s.Days)
            .Any(d => d.EffectiveLimit.HasValue);

        int colDate = 1;
        int colCatBase = 2;
        int colTotal = colCatBase + allCats.Count;
        int colIncome = colTotal + 1;
        int colNet = colIncome + 1;
        int colLimit = hasLimit ? colNet + 1 : 0;
        int colLimitDiff = hasLimit ? colNet + 2 : 0;
        int colBalance = hasLimit ? colNet + 3 : colNet + 1;
        int lastCol = colBalance;

        ws.Cell(1, colDate).Value = "Date";
        for (int i = 0; i < allCats.Count; i++)
            ws.Cell(1, colCatBase + i).Value = allCats[i].CategoryName;
        ws.Cell(1, colTotal).Value = "Total Expenses";
        ws.Cell(1, colIncome).Value = "Income";
        ws.Cell(1, colNet).Value = "Net";
        if (hasLimit)
        {
            ws.Cell(1, colLimit).Value = "Limit";
            ws.Cell(1, colLimitDiff).Value = "Limit Diff";
        }
        ws.Cell(1, colBalance).Value = "Balance";
        var headerRange = ws.Range(1, 1, 1, lastCol);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        int row = 2;
        decimal runningBalance = summaries.Count > 0 ? summaries[0].OpeningBalance : 0m;

        decimal allTotalExp = 0m;
        decimal allTotalInc = 0m;
        decimal allLimitBudget = 0m;
        decimal allLimitDiff = 0m;
        var allCatTotals = allCats.ToDictionary(c => c.CategoryId, _ => 0m);

        foreach (var summary in summaries)
        {
            ws.Cell(row, colDate).Value = $"{summary.Year}-{summary.Month:D2}";
            var monthHeaderRange = ws.Range(row, 1, row, lastCol);
            monthHeaderRange.Style.Font.Bold = true;
            monthHeaderRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
            row++;

            for (int r = 0; r < summary.Days.Count; r++, row++)
            {
                var day = summary.Days[r];
                runningBalance += day.TotalIncome - day.TotalExpenses;

                ws.Cell(row, colDate).Value = day.Date.ToString("yyyy-MM-dd");

                for (int i = 0; i < allCats.Count; i++)
                {
                    var match = day.ExpensesByCategory
                        .FirstOrDefault(e => e.CategoryId == allCats[i].CategoryId);
                    ws.Cell(row, colCatBase + i).Value = (double)(match?.Amount ?? 0m);
                }

                ws.Cell(row, colTotal).Value = (double)day.TotalExpenses;
                ws.Cell(row, colIncome).Value = (double)day.TotalIncome;
                ws.Cell(row, colNet).Value = (double)day.Net;
                if (hasLimit)
                {
                    if (day.EffectiveLimit.HasValue)
                        ws.Cell(row, colLimit).Value = (double)day.EffectiveLimit.Value;
                    if (day.LimitDiff.HasValue)
                        ws.Cell(row, colLimitDiff).Value = (double)day.LimitDiff.Value;
                }
                ws.Cell(row, colBalance).Value = (double)runningBalance;
            }

            ws.Cell(row, colDate).Value = "Total";
            for (int i = 0; i < allCats.Count; i++)
            {
                var cat = summary.MonthTotals.ExpensesByCategory
                    .FirstOrDefault(c => c.CategoryId == allCats[i].CategoryId);
                ws.Cell(row, colCatBase + i).Value = (double)(cat?.Amount ?? 0m);
            }
            ws.Cell(row, colTotal).Value = (double)summary.MonthTotals.TotalExpenses;
            ws.Cell(row, colIncome).Value = (double)summary.MonthTotals.TotalIncome;
            ws.Cell(row, colNet).Value = (double)summary.MonthTotals.Net;
            if (hasLimit)
            {
                ws.Cell(row, colLimit).Value = (double)summary.MonthTotals.AllowedMonthlyBudget;
                ws.Cell(row, colLimitDiff).Value = (double)summary.MonthTotals.TotalLimitDiff;
            }
            ws.Cell(row, colBalance).Value = (double)runningBalance;
            ws.Range(row, 1, row, lastCol).Style.Font.Bold = true;
            row++;

            allTotalExp += summary.MonthTotals.TotalExpenses;
            allTotalInc += summary.MonthTotals.TotalIncome;
            allLimitBudget += summary.MonthTotals.AllowedMonthlyBudget;
            allLimitDiff += summary.MonthTotals.TotalLimitDiff;
            foreach (var cat in summary.MonthTotals.ExpensesByCategory)
                if (allCatTotals.ContainsKey(cat.CategoryId))
                    allCatTotals[cat.CategoryId] += cat.Amount;
        }

        ws.Cell(row, colDate).Value = "All Time";
        for (int i = 0; i < allCats.Count; i++)
            ws.Cell(row, colCatBase + i).Value = (double)allCatTotals[allCats[i].CategoryId];
        ws.Cell(row, colTotal).Value = (double)allTotalExp;
        ws.Cell(row, colIncome).Value = (double)allTotalInc;
        ws.Cell(row, colNet).Value = (double)(allTotalInc - allTotalExp);
        if (hasLimit)
        {
            ws.Cell(row, colLimit).Value = (double)allLimitBudget;
            ws.Cell(row, colLimitDiff).Value = (double)allLimitDiff;
        }
        ws.Cell(row, colBalance).Value = (double)runningBalance;
        var allTimeRange = ws.Range(row, 1, row, lastCol);
        allTimeRange.Style.Font.Bold = true;
        allTimeRange.Style.Fill.BackgroundColor = XLColor.LightYellow;

        ws.Columns().AdjustToContents();

        using var ms2 = new MemoryStream();
        wb.SaveAs(ms2);
        return ms2.ToArray();
    }
}
