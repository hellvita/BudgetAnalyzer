using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetAnalyzer.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentToken _currentToken;

    public UsersController(UserService userService, ICurrentUser currentUser, ICurrentToken currentToken)
    {
        _userService = userService;
        _currentUser = currentUser;
        _currentToken = currentToken;
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteOwnAccount(CancellationToken ct)
    {
        await _userService.DeleteAccountAsync(_currentUser.UserId, _currentToken.Jti, _currentToken.ExpiresAt, ct);
        return NoContent();
    }
}
