using BudgetAnalyzer.Domain.Entities;

namespace BudgetAnalyzer.Application.Abstractions;

public record JwtResult(string Token, DateTime ExpiresAt);

public interface IJwtTokenService
{
    JwtResult Issue(User user);
}
