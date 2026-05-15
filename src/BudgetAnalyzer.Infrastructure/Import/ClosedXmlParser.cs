using BudgetAnalyzer.Application.Import;
using BudgetAnalyzer.Application.Import.Dtos;
using BudgetAnalyzer.Domain.Exceptions;
using ClosedXML.Excel;

namespace BudgetAnalyzer.Infrastructure.Import;

public class ClosedXmlParser : IXlsxParser
{
    public IReadOnlyList<ParsedColumnDto> DetectColumns(string filePath)
    {
        using var wb = new XLWorkbook(filePath);
        var ws = GetSingleWorksheet(wb);

        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

        if (lastCol == 0 || lastRow < 2) return Array.Empty<ParsedColumnDto>();

        var result = new List<ParsedColumnDto>();

        for (int col = 1; col <= lastCol; col++)
        {
            var header = ws.Cell(1, col).GetString().Trim();

            var samples = new List<string>();
            for (int row = 2; row <= lastRow && samples.Count < 3; row++)
            {
                var cell = ws.Cell(row, col);
                if (!cell.IsEmpty())
                    samples.Add(cell.GetString());
            }

            if (samples.Count > 0)
            {
                result.Add(new ParsedColumnDto(
                    Index: col - 1,
                    Letter: XLHelper.GetColumnLetterFromNumber(col),
                    Header: header,
                    Samples: samples.ToArray()
                ));
            }
        }

        return result;
    }

    public (IReadOnlyList<RawImportRow> Rows, int SkippedRows) ReadRows(
        string filePath, ColumnMappingDto mapping)
    {
        using var wb = new XLWorkbook(filePath);
        var ws = GetSingleWorksheet(wb);

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        var rows = new List<RawImportRow>();
        int skipped = 0;

        int dateCol = mapping.DateColumnIndex + 1;
        int incomeCol = mapping.IncomeColumnIndex + 1;
        var catCols = mapping.CategoryColumnIndexes.Select(i => i + 1).ToList();

        for (int row = 2; row <= lastRow; row++)
        {
            if (!TryParseDate(ws.Cell(row, dateCol), out var date))
            {
                skipped++;
                continue;
            }

            var catAmounts = new Dictionary<int, decimal>();
            bool rowIsValid = true;

            foreach (var catCol in catCols)
            {
                if (!TryParseAmount(ws.Cell(row, catCol), out var raw))
                {
                    skipped++;
                    rowIsValid = false;
                    break;
                }
                var transformed = ApplyTransform(raw, mapping.ScaleFactor, mapping.InvertSign);
                catAmounts[catCol - 1] = transformed;
            }

            if (!rowIsValid) continue;

            if (!TryParseAmount(ws.Cell(row, incomeCol), out var rawIncome))
            {
                skipped++;
                continue;
            }
            var income = ApplyTransform(rawIncome, mapping.ScaleFactor, mapping.InvertSign);

            rows.Add(new RawImportRow(
                Date: date,
                CategoryAmounts: catAmounts,
                Income: income
            ));
        }

        return (rows, skipped);
    }

    private static IXLWorksheet GetSingleWorksheet(XLWorkbook wb)
    {
        if (wb.Worksheets.Count != 1)
            throw new ValidationException(
                $"The file must contain exactly one sheet. Found {wb.Worksheets.Count}.");
        return wb.Worksheets.First();
    }

    private static bool TryParseDate(IXLCell cell, out DateOnly date)
    {
        date = default;
        if (cell.IsEmpty()) return false;

        if (cell.DataType == XLDataType.Number || cell.DataType == XLDataType.DateTime)
        {
            try
            {
                var dt = DateTime.FromOADate(cell.GetDouble());
                date = DateOnly.FromDateTime(dt);
                return true;
            }
            catch { /* fall through */ }
        }

        return DateOnly.TryParse(cell.GetString().Trim(), out date);
    }

    private static bool TryParseAmount(IXLCell cell, out decimal amount)
    {
        amount = 0;
        if (cell.IsEmpty()) return true;

        if (cell.DataType == XLDataType.Number)
        {
            amount = (decimal)cell.GetDouble();
            return true;
        }

        return decimal.TryParse(
            cell.GetString().Trim(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out amount);
    }

    private static decimal ApplyTransform(decimal raw, decimal scale, bool invert) =>
        raw * scale * (invert ? -1m : 1m);
}
