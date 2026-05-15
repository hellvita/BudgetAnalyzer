using BudgetAnalyzer.Application.Import.Dtos;

namespace BudgetAnalyzer.Application.Import;

public record RawImportRow(
    DateOnly Date,
    IReadOnlyDictionary<int, decimal> CategoryAmounts,
    decimal Income
);

public interface IXlsxParser
{
    IReadOnlyList<ParsedColumnDto> DetectColumns(string filePath);

    (IReadOnlyList<RawImportRow> Rows, int SkippedRows)
        ReadRows(string filePath, ColumnMappingDto mapping);
}
