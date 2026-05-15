using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetAnalyzer.Api.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly IExportService _exportService;
    private readonly ICurrentUser _currentUser;

    public ExportController(IExportService exportService, ICurrentUser currentUser)
    {
        _exportService = exportService;
        _currentUser   = currentUser;
    }

    [HttpGet("month/{yearMonth}")]
    public async Task<IActionResult> ExportMonth(string yearMonth, CancellationToken ct)
    {
        if (!TryParseYearMonth(yearMonth, out int year, out int month))
            return BadRequest("Invalid format. Expected yyyy-MM (e.g. 2026-05).");

        var bytes = await _exportService.GenerateMonthXlsxAsync(
            _currentUser.UserId, year, month, ct);

        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"budget-{yearMonth}.xlsx");
    }

    private static bool TryParseYearMonth(string s, out int year, out int month)
    {
        year = month = 0;
        var parts = s.Split('-');
        return parts.Length == 2
            && int.TryParse(parts[0], out year)
            && int.TryParse(parts[1], out month)
            && month is >= 1 and <= 12;
    }
}
