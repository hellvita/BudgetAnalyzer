using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetAnalyzer.Api.Controllers;

[ApiController]
[Route("api")]
public class PingController : ControllerBase
{
    [Authorize]
    [HttpGet("ping")]
    public IActionResult Ping() => Ok();
}
