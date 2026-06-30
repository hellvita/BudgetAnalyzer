namespace BudgetAnalyzer.Infrastructure.Auth;

public class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>BCrypt work factor (cost). Default 11 matches the previous hardcoded value.</summary>
    public int BcryptWorkFactor { get; set; } = 11;
}
