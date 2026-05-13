using BudgetAnalyzer.Application.Abstractions;

namespace BudgetAnalyzer.Infrastructure.Auth;

public class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;

    public string Hash(string raw) => BCrypt.Net.BCrypt.HashPassword(raw, WorkFactor);

    public bool Verify(string raw, string hash) => BCrypt.Net.BCrypt.Verify(raw, hash);
}
