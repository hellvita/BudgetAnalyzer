using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetAnalyzer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var response = await _authService.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var response = await _authService.LoginAsync(request, ct);
        return Ok(response);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(
        [FromServices] ITokenRevocationService tokenRevocation,
        [FromServices] ICurrentToken currentToken,
        [FromServices] IUnitOfWork uow,
        CancellationToken ct)
    {
        tokenRevocation.Stage(currentToken.Jti, currentToken.ExpiresAt);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }
}
