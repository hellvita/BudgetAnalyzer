namespace BudgetAnalyzer.Application.Abstractions;

public interface ITokenRevocationService
{
    void Stage(string jti, DateTime expiresAt);
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default);
    Task DeleteExpiredAsync(CancellationToken ct = default);
}
