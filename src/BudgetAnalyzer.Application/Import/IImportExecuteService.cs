using BudgetAnalyzer.Application.Import.Dtos;

namespace BudgetAnalyzer.Application.Import;

public interface IImportExecuteService
{
    Task<ImportResultDto> ExecuteAsync(
        Guid userId,
        ColumnMappingDto mapping,
        CancellationToken ct = default);
}
