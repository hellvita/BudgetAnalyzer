using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BudgetAnalyzer.Application.Abstractions;

namespace BudgetAnalyzer.Api.Auth;

public class CurrentToken : ICurrentToken
{
    public string Jti { get; }
    public DateTime ExpiresAt { get; }

    public CurrentToken(IHttpContextAccessor accessor)
    {
        var user = accessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException("No authenticated user in context.");

        Jti = user.FindFirstValue(JwtRegisteredClaimNames.Jti)
            ?? throw new UnauthorizedAccessException("Token is missing jti claim.");

        var exp = user.FindFirstValue(JwtRegisteredClaimNames.Exp)
            ?? throw new UnauthorizedAccessException("Token is missing exp claim.");

        ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(exp)).UtcDateTime;
    }
}
