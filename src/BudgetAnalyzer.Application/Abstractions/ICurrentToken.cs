namespace BudgetAnalyzer.Application.Abstractions;

public interface ICurrentToken
{
    string Jti { get; }
    DateTime ExpiresAt { get; }
}
