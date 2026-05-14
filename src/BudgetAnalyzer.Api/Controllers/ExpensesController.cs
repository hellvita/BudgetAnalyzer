using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Expenses;
using BudgetAnalyzer.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetAnalyzer.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize]
public class ExpensesController : ControllerBase
{
    private readonly ExpenseService _expenseService;
    private readonly ICurrentUser _currentUser;

    public ExpensesController(ExpenseService expenseService, ICurrentUser currentUser)
    {
        _expenseService = expenseService;
        _currentUser = currentUser;
    }

    [HttpPut("{date}/{categoryId:guid}")]
    public async Task<IActionResult> Upsert(
        DateOnly date,
        Guid categoryId,
        [FromBody] UpsertExpenseRequest request,
        CancellationToken ct)
    {
        await _expenseService.UpsertAsync(_currentUser.UserId, categoryId, date, request.Amount!.Value, ct);
        return NoContent();
    }

    [HttpDelete("{date}/{categoryId:guid}")]
    public async Task<IActionResult> Delete(
        DateOnly date,
        Guid categoryId,
        CancellationToken ct)
    {
        await _expenseService.DeleteAsync(_currentUser.UserId, categoryId, date, ct);
        return NoContent();
    }

    [HttpGet("by-date/{date}")]
    public async Task<ActionResult<ExpenseByDateResponse>> GetByDate(
        DateOnly date,
        CancellationToken ct)
    {
        var result = await _expenseService.GetByDateAsync(_currentUser.UserId, date, ct);
        return Ok(result);
    }

    [HttpGet("by-month/{yearMonth}")]
    public async Task<ActionResult<List<ExpenseByMonthDayItem>>> GetByMonth(
        string yearMonth,
        CancellationToken ct)
    {
        if (!TryParseYearMonth(yearMonth, out var year, out var month))
            throw new ValidationException("yearMonth must be in yyyy-MM format.");

        var result = await _expenseService.GetByMonthAsync(_currentUser.UserId, year, month, ct);
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
