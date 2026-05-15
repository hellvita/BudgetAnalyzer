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

        // Column positions (1-based)
        int colDate    = 1;
        int colCatBase = 2;
        int colTotal   = colCatBase + cats.Count;
        int colIncome  = colTotal + 1;
        int colNet     = colIncome + 1;
        int colBalance = colNet + 1;

        // Header row
        ws.Cell(1, colDate).Value = "Date";
        for (int i = 0; i < cats.Count; i++)
            ws.Cell(1, colCatBase + i).Value = cats[i].CategoryName;
        ws.Cell(1, colTotal).Value   = "Total Expenses";
        ws.Cell(1, colIncome).Value  = "Income";
        ws.Cell(1, colNet).Value     = "Net";
        ws.Cell(1, colBalance).Value = "Balance";

        var headerRange = ws.Range(1, 1, 1, colBalance);
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

            ws.Cell(row, colTotal).Value   = (double)day.TotalExpenses;
            ws.Cell(row, colIncome).Value  = (double)day.TotalIncome;
            ws.Cell(row, colNet).Value     = (double)day.Net;
            ws.Cell(row, colBalance).Value = (double)runningBalance;
        }

        // Totals row
        int totalsRow = summary.Days.Count + 2;
        ws.Cell(totalsRow, colDate).Value = "Total";

        for (int i = 0; i < cats.Count; i++)
            ws.Cell(totalsRow, colCatBase + i).Value = (double)cats[i].Amount;

        ws.Cell(totalsRow, colTotal).Value   = (double)summary.MonthTotals.TotalExpenses;
        ws.Cell(totalsRow, colIncome).Value  = (double)summary.MonthTotals.TotalIncome;
        ws.Cell(totalsRow, colNet).Value     = (double)summary.MonthTotals.Net;
        ws.Cell(totalsRow, colBalance).Value = (double)runningBalance;

        var totalsRange = ws.Range(totalsRow, 1, totalsRow, colBalance);
        totalsRange.Style.Font.Bold = true;

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
