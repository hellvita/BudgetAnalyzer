using BudgetAnalyzer.Domain.Entities;

namespace BudgetAnalyzer.Application.Abstractions;

public interface IJwtTokenService
{
    string Issue(User user);
}
