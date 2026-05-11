namespace BudgetAnalyzer.Application.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly Today();
}
