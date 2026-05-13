using System.Security.Claims;
using BudgetAnalyzer.Application.Abstractions;

namespace BudgetAnalyzer.Api.Auth;

public class CurrentUser : ICurrentUser
{
    public Guid UserId { get; }

    public CurrentUser(IHttpContextAccessor accessor)
    {
        var value = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("No authenticated user in context.");
        UserId = Guid.Parse(value);
    }
}
