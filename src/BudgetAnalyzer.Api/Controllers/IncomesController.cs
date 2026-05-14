using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Incomes;
using BudgetAnalyzer.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetAnalyzer.Api.Controllers;

[ApiController]
[Route("api/incomes")]
[Authorize]
public class IncomesController : ControllerBase
{
    private readonly IncomeService _incomeService;
    private readonly ICurrentUser _currentUser;

    public IncomesController(IncomeService incomeService, ICurrentUser currentUser)
    {
        _incomeService = incomeService;
        _currentUser = currentUser;
    }

    [HttpPut("{date}")]
    public async Task<IActionResult> Upsert(
        DateOnly date,
        [FromBody] UpsertIncomeRequest request,
        CancellationToken ct)
    {
        await _incomeService.UpsertAsync(_currentUser.UserId, date, request.Amount!.Value, ct);
        return NoContent();
    }

    [HttpDelete("{date}")]
    public async Task<IActionResult> Delete(DateOnly date, CancellationToken ct)
    {
        await _incomeService.DeleteAsync(_currentUser.UserId, date, ct);
        return NoContent();
    }

    [HttpGet("by-month/{yearMonth}")]
    public async Task<ActionResult<List<IncomeByMonthDayItem>>> GetByMonth(
        string yearMonth,
        CancellationToken ct)
    {
        if (!TryParseYearMonth(yearMonth, out var year, out var month))
            throw new ValidationException("yearMonth must be in yyyy-MM format.");

        var result = await _incomeService.GetByMonthAsync(_currentUser.UserId, year, month, ct);
        return Ok(result);
    }

    private static bool TryParseYearMonth(string value, out int year, out int month)
    {
        year = 0;
        month = 0;
        if (value.Length != 7 || value[4] != '-')
            return false;
        return int.TryParse(value.AsSpan(0, 4), out year)
            && int.TryParse(value.AsSpan(5, 2), out month)
            && month is >= 1 and <= 12;
    }
}
