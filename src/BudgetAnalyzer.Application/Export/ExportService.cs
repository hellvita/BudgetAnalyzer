using System.IO.Compression;
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

    public async Task<byte[]> GenerateAllMonthsZipAsync(
        Guid userId, CancellationToken ct = default)
    {
        var months = await _summaryService.GetMonthsWithDataAsync(userId, ct);
        if (months.Count == 0) return Array.Empty<byte>();

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (year, month) in months)
            {
                var summary = await _summaryService.GetMonthAsync(userId, year, month, ct);
                var xlsx = _renderer.RenderMonth(summary);
                var entry = zip.CreateEntry(
                    $"budget-{year}-{month:D2}.xlsx",
                    CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(xlsx, ct);
            }
        }
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateAllMonthsCombinedAsync(
        Guid userId, CancellationToken ct = default)
    {
        var months = await _summaryService.GetMonthsWithDataAsync(userId, ct);

        var summaries = new List<MonthSummaryResponse>(months.Count);
        foreach (var (year, month) in months)
            summaries.Add(await _summaryService.GetMonthAsync(userId, year, month, ct));

        return _renderer.RenderAllMonthsCombined(summaries);
    }
}
