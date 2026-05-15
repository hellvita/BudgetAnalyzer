namespace BudgetAnalyzer.Application.Import.Dtos;

public record PreviewResultDto(
    int TotalDataRows,
    int SkippedRows,
    IReadOnlyList<PreviewRowDto> Preview
);
