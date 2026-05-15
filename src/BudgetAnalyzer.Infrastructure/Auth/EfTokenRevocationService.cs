using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Domain.Entities;
using BudgetAnalyzer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetAnalyzer.Infrastructure.Auth;

public class EfTokenRevocationService : ITokenRevocationService
{
    private readonly AppDbContext _db;

    public EfTokenRevocationService(AppDbContext db)
    {
        _db = db;
    }

    public void Stage(string jti, DateTime expiresAt)
        => _db.RevokedTokens.Add(new RevokedToken { Jti = jti, ExpiresAt = expiresAt });

    public Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default)
        => _db.RevokedTokens.AnyAsync(t => t.Jti == jti, ct);

    public Task DeleteExpiredAsync(CancellationToken ct = default)
        => _db.RevokedTokens
            .Where(t => t.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);
}
