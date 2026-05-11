namespace BudgetAnalyzer.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string raw);
    bool Verify(string raw, string hash);
}
