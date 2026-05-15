using BudgetAnalyzer.Application.Import.Dtos;

namespace BudgetAnalyzer.Application.Import;

public interface IImportParseService
{
    Task<ParseResultDto> ParseAsync(Stream fileStream, CancellationToken ct = default);
}
