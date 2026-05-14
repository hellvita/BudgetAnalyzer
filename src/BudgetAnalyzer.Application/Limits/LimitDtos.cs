using System.ComponentModel.DataAnnotations;

namespace BudgetAnalyzer.Application.Limits;

public record UpsertLimitRequest([Required][Range(0, double.MaxValue)] decimal? Amount);

public record LimitHistoryItem(DateOnly EffectiveFromDate, decimal Amount);
