namespace BudgetAnalyzer.Application.Import.Dtos;

public record PreviewExpenseDto(string CategoryName, decimal Amount);

public record PreviewRowDto(
    DateOnly Date,
    IReadOnlyList<PreviewExpenseDto> Expenses,
    decimal Income
);
