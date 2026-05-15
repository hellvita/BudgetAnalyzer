using BudgetAnalyzer.Application.Summaries;

namespace BudgetAnalyzer.Application.Export;

public class ExportService : IExportService
{
    private readonly SummaryService _summaryService;
    private readonly IExportRenderer _renderer;

    public ExportService(SummaryService summaryService, IExportRenderer renderer)
    {
        _summaryService = summaryService;
        _renderer = renderer;
    }

    public async Task<byte[]> GenerateMonthXlsxAsync(
        Guid userId, int year, int month, CancellationToken ct = default)
    {
        var summary = await _summaryService.GetMonthAsync(userId, year, month, ct);
        return _renderer.RenderMonth(summary);
    }
}
