using BudgetAnalyzer.Application.Import.Dtos;
using BudgetAnalyzer.Domain.Exceptions;

namespace BudgetAnalyzer.Application.Import;

public class ImportPreviewService : IImportPreviewService
{
    private const int MaxPreviewRows = 10;

    private readonly ITempFileStore _store;
    private readonly IXlsxParser _parser;

    public ImportPreviewService(ITempFileStore store, IXlsxParser parser)
    {
        _store = store;
        _parser = parser;
    }

    public Task<PreviewResultDto> PreviewAsync(ColumnMappingDto mapping, CancellationToken ct = default)
    {
        if (!_store.Exists(mapping.FileId))
            throw new NotFoundException(
                $"Import file '{mapping.FileId}' not found. Please upload the file again.");

        var path = _store.GetFilePath(mapping.FileId);
        var columns = _parser.DetectColumns(path);
        var headerByIndex = columns.ToDictionary(c => c.Index, c => c.Header);

        var (allRows, skipped) = _parser.ReadRows(path, mapping);

        var preview = allRows
            .Take(MaxPreviewRows)
            .Select(row => new PreviewRowDto(
                Date: row.Date,
                Expenses: mapping.CategoryColumnIndexes
                    .Select(idx => new PreviewExpenseDto(
                        CategoryName: headerByIndex.GetValueOrDefault(idx, $"Column {idx}"),
                        Amount: row.CategoryAmounts.GetValueOrDefault(idx, 0m)))
                    .ToList(),
                Income: row.Income))
            .ToList();

        return Task.FromResult(new PreviewResultDto(
            TotalDataRows: allRows.Count,
            SkippedRows: skipped,
            Preview: preview));
    }
}
