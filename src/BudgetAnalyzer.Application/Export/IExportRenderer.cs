using BudgetAnalyzer.Application.Summaries;

namespace BudgetAnalyzer.Application.Export;

public interface IExportRenderer
{
    byte[] RenderMonth(MonthSummaryResponse summary);
    byte[] RenderAllMonthsCombined(IReadOnlyList<MonthSummaryResponse> summaries);
}
