using System.ComponentModel.DataAnnotations;

namespace BudgetAnalyzer.Application.Expenses;

public record UpsertExpenseRequest([Required][Range(0, double.MaxValue)] decimal? Amount);

public record ExpenseByCategoryItem(Guid CategoryId, string CategoryName, decimal Amount);

public record ExpenseByDateResponse(DateOnly Date, IReadOnlyList<ExpenseByCategoryItem> PerCategory, decimal DailyTotal);

public record ExpenseMonthPerCategory(Guid CategoryId, decimal Amount);

public record ExpenseByMonthDayItem(DateOnly Date, IReadOnlyList<ExpenseMonthPerCategory> PerCategory, decimal DailyTotal);
