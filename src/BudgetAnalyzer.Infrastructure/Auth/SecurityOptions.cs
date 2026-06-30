namespace BudgetAnalyzer.Infrastructure.Auth;

public class SecurityOptions
{
    public const string SectionName = "Security";

    public int BcryptWorkFactor { get; set; } = 11;
}
