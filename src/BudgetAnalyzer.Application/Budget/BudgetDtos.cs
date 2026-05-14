using System.ComponentModel.DataAnnotations;

namespace BudgetAnalyzer.Application.Budget;

public record GetBudgetResponse(decimal InitialBudget);

public record SetBudgetRequest([Required][Range(0, double.MaxValue)] decimal? InitialBudget);
