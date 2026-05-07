namespace BudgetAnalyzer.Domain.Entities;

public class DailyLimit
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly EffectiveFromDate { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}
