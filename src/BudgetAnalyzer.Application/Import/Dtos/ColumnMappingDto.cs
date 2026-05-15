namespace BudgetAnalyzer.Application.Import.Dtos;

public record ColumnMappingDto(
    string FileId,
    int DateColumnIndex,
    IReadOnlyList<int> CategoryColumnIndexes,
    int IncomeColumnIndex,
    decimal ScaleFactor,
    bool InvertSign
);
