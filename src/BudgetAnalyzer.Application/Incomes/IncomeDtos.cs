using System.ComponentModel.DataAnnotations;

namespace BudgetAnalyzer.Application.Incomes;

public record UpsertIncomeRequest([Required][Range(0, double.MaxValue)] decimal? Amount);

public record IncomeByMonthDayItem(DateOnly Date, decimal Amount);
