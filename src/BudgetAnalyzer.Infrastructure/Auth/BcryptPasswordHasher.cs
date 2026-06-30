using BudgetAnalyzer.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace BudgetAnalyzer.Infrastructure.Auth;

public class BcryptPasswordHasher(IOptions<SecurityOptions> options) : IPasswordHasher
{
    private readonly int _workFactor = options.Value.BcryptWorkFactor;

    public string Hash(string raw) => BCrypt.Net.BCrypt.HashPassword(raw, _workFactor);

    public bool Verify(string raw, string hash) => BCrypt.Net.BCrypt.Verify(raw, hash);
}
