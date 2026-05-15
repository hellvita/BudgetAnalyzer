namespace BudgetAnalyzer.Application.Import.Dtos;

public record ParsedColumnDto(
    int Index,
    string Letter,
    string Header,
    string[] Samples
);
