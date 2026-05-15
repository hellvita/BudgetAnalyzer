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

    public UsersController(UserService userService, ICurrentUser currentUser)
    {
        _userService = userService;
        _currentUser = currentUser;
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteOwnAccount(CancellationToken ct)
    {
        await _userService.DeleteAccountAsync(_currentUser.UserId, ct);
        return NoContent();
    }
}
