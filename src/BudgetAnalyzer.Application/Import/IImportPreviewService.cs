using BudgetAnalyzer.Application.Import.Dtos;

namespace BudgetAnalyzer.Application.Import;

public interface IImportPreviewService
{
    Task<PreviewResultDto> PreviewAsync(ColumnMappingDto mapping, CancellationToken ct = default);
}
