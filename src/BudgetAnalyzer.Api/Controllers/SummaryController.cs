using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Summaries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetAnalyzer.Api.Controllers;

[ApiController]
[Route("api/summary")]
[Authorize]
public class SummaryController : ControllerBase
{
    private readonly SummaryService _summaryService;
    private readonly ICurrentUser _currentUser;

    public SummaryController(SummaryService summaryService, ICurrentUser currentUser)
    {
        _summaryService = summaryService;
        _currentUser = currentUser;
    }

    [HttpGet("day/{date}")]
    public async Task<ActionResult<DaySummaryResponse>> GetDay(DateOnly date, CancellationToken ct)
    {
        var result = await _summaryService.GetDayAsync(_currentUser.UserId, date, ct);
        return Ok(result);
    }

    [HttpGet("month/{yearMonth}")]
    public async Task<ActionResult<MonthSummaryResponse>> GetMonth(string yearMonth, CancellationToken ct)
    {
        if (!TryParseYearMonth(yearMonth, out var year, out var month))
            return BadRequest("Invalid month format. Use yyyy-MM.");

        var result = await _summaryService.GetMonthAsync(_currentUser.UserId, year, month, ct);
        return Ok(result);
    }

    [HttpGet("all-time")]
    public async Task<ActionResult<AllTimeSummaryResponse>> GetAllTime(CancellationToken ct)
    {
        var result = await _summaryService.GetAllTimeAsync(_currentUser.UserId, ct);
        return Ok(result);
    }

    private static bool TryParseYearMonth(string value, out int year, out int month)
    {
        year = 0;
        month = 0;
        var parts = value.Split('-');
        return parts.Length == 2
            && int.TryParse(parts[0], out year)
            && int.TryParse(parts[1], out month)
            && month >= 1 && month <= 12;
    }
}
