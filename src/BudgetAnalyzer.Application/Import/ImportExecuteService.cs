using BudgetAnalyzer.Application.Categories;
using BudgetAnalyzer.Application.Expenses;
using BudgetAnalyzer.Application.Import.Dtos;
using BudgetAnalyzer.Application.Incomes;
using BudgetAnalyzer.Domain.Exceptions;

namespace BudgetAnalyzer.Application.Import;

public class ImportExecuteService : IImportExecuteService
{
    private readonly ITempFileStore _store;
    private readonly IXlsxParser _parser;
    private readonly CategoryService _categoryService;
    private readonly ExpenseService _expenseService;
    private readonly IncomeService _incomeService;

    public ImportExecuteService(
        ITempFileStore store,
        IXlsxParser parser,
        CategoryService categoryService,
        ExpenseService expenseService,
        IncomeService incomeService)
    {
        _store = store;
        _parser = parser;
        _categoryService = categoryService;
        _expenseService = expenseService;
        _incomeService = incomeService;
    }

    public async Task<ImportResultDto> ExecuteAsync(
        Guid userId, ColumnMappingDto mapping, CancellationToken ct = default)
    {
        if (!_store.Exists(mapping.FileId))
            throw new NotFoundException(
                $"Import file '{mapping.FileId}' not found. Please upload the file again.");

        var path = _store.GetFilePath(mapping.FileId);
        var columns = _parser.DetectColumns(path);
        var headerByIndex = columns.ToDictionary(c => c.Index, c => c.Header);

        var (rows, skipped) = _parser.ReadRows(path, mapping);

        var uniqueNames = mapping.CategoryColumnIndexes
            .Select(idx => headerByIndex.GetValueOrDefault(idx, $"Column {idx}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var categoryIdByName = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var created = new List<string>();

        foreach (var name in uniqueNames)
        {
            var (id, wasCreated) = await _categoryService.GetOrCreateAsync(userId, name, ct);
            categoryIdByName[name] = id;
            if (wasCreated) created.Add(name);
        }

        int expensesUpserted = 0, incomesUpserted = 0;
        var datesImported = new HashSet<DateOnly>();

        foreach (var row in rows)
        {
            bool rowHadData = false;

            foreach (var idx in mapping.CategoryColumnIndexes)
            {
                var amount = row.CategoryAmounts.GetValueOrDefault(idx, 0m);
                if (amount == 0m) continue;

                var name = headerByIndex.GetValueOrDefault(idx, $"Column {idx}");
                var categoryId = categoryIdByName[name];
                await _expenseService.UpsertAsync(userId, categoryId, row.Date, amount, ct);
                expensesUpserted++;
                rowHadData = true;
            }

            if (row.Income != 0m)
            {
                await _incomeService.UpsertAsync(userId, row.Date, row.Income, ct);
                incomesUpserted++;
                rowHadData = true;
            }

            if (rowHadData) datesImported.Add(row.Date);
        }

        _store.Delete(mapping.FileId);

        return new ImportResultDto(
            DaysImported: datesImported.Count,
            RowsSkipped: skipped,
            CategoriesCreated: created,
            ExpensesUpserted: expensesUpserted,
            IncomesUpserted: incomesUpserted);
    }
}
