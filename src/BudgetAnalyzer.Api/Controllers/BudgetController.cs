using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Budget;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetAnalyzer.Api.Controllers;

[ApiController]
[Route("api/me/budget")]
[Authorize]
public class BudgetController : ControllerBase
{
    private readonly BudgetService _budgetService;
    private readonly ICurrentUser _currentUser;

    public BudgetController(BudgetService budgetService, ICurrentUser currentUser)
    {
        _budgetService = budgetService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<GetBudgetResponse>> Get(CancellationToken ct)
    {
        var amount = await _budgetService.GetAsync(_currentUser.UserId, ct);
        return Ok(new GetBudgetResponse(amount));
    }

    [HttpPut]
    public async Task<ActionResult<GetBudgetResponse>> Set([FromBody] SetBudgetRequest request, CancellationToken ct)
    {
        await _budgetService.SetAsync(_currentUser.UserId, request.InitialBudget!.Value, ct);
        return Ok(new GetBudgetResponse(request.InitialBudget.Value));
    }
}
