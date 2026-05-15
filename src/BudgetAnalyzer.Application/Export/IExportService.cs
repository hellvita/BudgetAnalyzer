namespace BudgetAnalyzer.Application.Export;

public interface IExportService
{
    Task<byte[]> GenerateMonthXlsxAsync(
        Guid userId, int year, int month, CancellationToken ct = default);
}
