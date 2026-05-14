using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Limits;
using BudgetAnalyzer.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetAnalyzer.Api.Controllers;

[ApiController]
[Route("api/limits")]
[Authorize]
public class LimitsController : ControllerBase
{
    private readonly LimitService _limitService;
    private readonly ICurrentUser _currentUser;

    public LimitsController(LimitService limitService, ICurrentUser currentUser)
    {
        _limitService = limitService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<LimitHistoryItem>>> GetHistory(CancellationToken ct)
    {
        var history = await _limitService.GetHistoryAsync(_currentUser.UserId, ct);
        return Ok(history);
    }

    [HttpPut("{effectiveFromDate}")]
    public async Task<IActionResult> Set(
        DateOnly effectiveFromDate,
        [FromBody] UpsertLimitRequest request,
        CancellationToken ct)
    {
        await _limitService.SetAsync(_currentUser.UserId, effectiveFromDate, request.Amount!.Value, ct);
        return NoContent();
    }

    [HttpDelete("{effectiveFromDate}")]
    public async Task<IActionResult> Delete(DateOnly effectiveFromDate, CancellationToken ct)
    {
        await _limitService.DeleteAsync(_currentUser.UserId, effectiveFromDate, ct);
        return NoContent();
    }
}
