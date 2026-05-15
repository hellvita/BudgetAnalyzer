namespace BudgetAnalyzer.Application.Import.Dtos;

public record ParseResultDto(
    string FileId,
    IReadOnlyList<ParsedColumnDto> Columns
);
