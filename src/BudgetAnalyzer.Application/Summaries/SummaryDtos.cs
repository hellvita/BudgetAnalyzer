namespace BudgetAnalyzer.Application.Summaries;

public record SummaryExpenseByCategory(Guid CategoryId, string CategoryName, decimal Amount);

public record DaySummaryResponse(
    DateOnly Date,
    decimal Income,
    IReadOnlyList<SummaryExpenseByCategory> ExpensesByCategory,
    decimal TotalExpenses,
    decimal? EffectiveLimit,
    decimal? LimitDiff,
    decimal Net);

public record MonthSummaryDayItem(
    DateOnly Date,
    decimal TotalExpenses,
    decimal TotalIncome,
    decimal? EffectiveLimit,
    decimal? LimitDiff,
    decimal Net,
    IReadOnlyList<SummaryExpenseByCategory> ExpensesByCategory);

public record MonthTotals(
    decimal TotalExpenses,
    decimal TotalIncome,
    IReadOnlyList<SummaryExpenseByCategory> ExpensesByCategory,
    decimal AllowedMonthlyBudget,
    decimal TotalLimitDiff,
    decimal Net);

public record MonthSummaryResponse(
    int Year,
    int Month,
    decimal OpeningBalance,
    IReadOnlyList<MonthSummaryDayItem> Days,
    MonthTotals MonthTotals);

public record AllTimeSummaryResponse(
    decimal InitialBudget,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal TotalLimitDiff,
    decimal CurrentBalance,
    decimal Net);
