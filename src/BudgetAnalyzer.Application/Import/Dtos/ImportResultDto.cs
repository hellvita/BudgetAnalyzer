namespace BudgetAnalyzer.Application.Import.Dtos;

public record ImportResultDto(
    int DaysImported,
    int RowsSkipped,
    IReadOnlyList<string> CategoriesCreated,
    int ExpensesUpserted,
    int IncomesUpserted
);
