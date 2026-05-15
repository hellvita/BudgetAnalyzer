namespace BudgetAnalyzer.Api.Controllers.Requests;

public record ColumnMappingRequest(
    string FileId,
    int DateColumnIndex,
    List<int> CategoryColumnIndexes,
    int IncomeColumnIndex,
    decimal ScaleFactor = 1m,
    bool InvertSign = false
);
