using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Categories;
using BudgetAnalyzer.Application.Expenses;
using BudgetAnalyzer.Application.Import;
using BudgetAnalyzer.Application.Import.Dtos;
using BudgetAnalyzer.Application.Incomes;
using BudgetAnalyzer.Domain.Entities;
using BudgetAnalyzer.Domain.Exceptions;
using BudgetAnalyzer.UnitTests.Infrastructure;
using Moq;

namespace BudgetAnalyzer.UnitTests.Import;

public class ImportExecuteServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private const string FileId = "test-file-id";
    private const string FilePath = "/fake/path/test-file.xlsx";

    private readonly Mock<ITempFileStore> _store = new();
    private readonly Mock<IXlsxParser> _parser = new();
    private readonly Mock<IRepository<Category>> _categoryRepo = new();
    private readonly Mock<IRepository<DailyExpense>> _expenseRepo = new();
    private readonly Mock<IRepository<DailyIncome>> _incomeRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private readonly List<Category> _categoryData = [];
    private readonly List<DailyExpense> _expenseData = [];
    private readonly List<DailyIncome> _incomeData = [];

    public ImportExecuteServiceTests()
    {
        _store.Setup(s => s.Exists(FileId)).Returns(true);
        _store.Setup(s => s.GetFilePath(FileId)).Returns(FilePath);
        _store.Setup(s => s.Delete(It.IsAny<string>()));

        _categoryRepo.Setup(r => r.Query()).Returns(() => _categoryData.AsAsyncQueryable());
        _categoryRepo.Setup(r => r.Add(It.IsAny<Category>())).Callback<Category>(c => _categoryData.Add(c));
        _categoryRepo.Setup(r => r.Update(It.IsAny<Category>()));

        _expenseRepo.Setup(r => r.Query()).Returns(() => _expenseData.AsAsyncQueryable());
        _expenseRepo.Setup(r => r.Add(It.IsAny<DailyExpense>())).Callback<DailyExpense>(e => _expenseData.Add(e));
        _expenseRepo.Setup(r => r.Update(It.IsAny<DailyExpense>()));

        _incomeRepo.Setup(r => r.Query()).Returns(() => _incomeData.AsAsyncQueryable());
        _incomeRepo.Setup(r => r.Add(It.IsAny<DailyIncome>())).Callback<DailyIncome>(i => _incomeData.Add(i));
        _incomeRepo.Setup(r => r.Update(It.IsAny<DailyIncome>()));

        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private ImportExecuteService CreateSut()
    {
        var categoryService = new CategoryService(_categoryRepo.Object, _uow.Object);
        var expenseService = new ExpenseService(_expenseRepo.Object, _categoryRepo.Object, _uow.Object);
        var incomeService = new IncomeService(_incomeRepo.Object, _uow.Object);
        return new ImportExecuteService(_store.Object, _parser.Object, categoryService, expenseService, incomeService);
    }

    private void SetupParser(
        IReadOnlyList<ParsedColumnDto> columns,
        IReadOnlyList<RawImportRow> rows,
        int skippedRows = 0)
    {
        _parser.Setup(p => p.DetectColumns(FilePath)).Returns(columns);
        _parser.Setup(p => p.ReadRows(FilePath, It.IsAny<ColumnMappingDto>())).Returns((rows, skippedRows));
    }

    private static ParsedColumnDto MakeCol(int idx, string header)
        => new(idx, ((char)('A' + idx)).ToString(), header, ["sample"]);

    private static ColumnMappingDto MakeMapping(int[] catCols)
        => new(FileId, 0, catCols.ToList(), 3, 1m, false);

    [Fact]
    public async Task Execute_NewCategories_AreReportedAsCreated()
    {
        var cols = new[] { MakeCol(0, "Date"), MakeCol(1, "Groceries"), MakeCol(2, "Transport"), MakeCol(3, "Income") };
        var date = new DateOnly(2026, 1, 1);
        var rows = new[]
        {
            new RawImportRow(date, new Dictionary<int, decimal> { [1] = 50m, [2] = 30m }, 0m)
        };
        SetupParser(cols, rows);

        var result = await CreateSut().ExecuteAsync(UserId, MakeMapping([1, 2]));

        Assert.Contains("Groceries", result.CategoriesCreated);
        Assert.Contains("Transport", result.CategoriesCreated);
        Assert.Equal(2, result.CategoriesCreated.Count);
    }

    [Fact]
    public async Task Execute_ExistingCategory_NotReportedAsCreated()
    {
        var existingId = Guid.NewGuid();
        _categoryData.Add(new Category { Id = existingId, UserId = UserId, Name = "Groceries", IsArchived = false });

        var cols = new[] { MakeCol(0, "Date"), MakeCol(1, "Groceries"), MakeCol(3, "Income") };
        var date = new DateOnly(2026, 1, 1);
        var rows = new[]
        {
            new RawImportRow(date, new Dictionary<int, decimal> { [1] = 50m }, 0m)
        };
        SetupParser(cols, rows);

        var result = await CreateSut().ExecuteAsync(UserId, MakeMapping([1]));

        Assert.Empty(result.CategoriesCreated);
    }

    [Fact]
    public async Task Execute_ZeroExpenseAmount_SkipsUpsert()
    {
        var cols = new[] { MakeCol(0, "Date"), MakeCol(1, "Groceries"), MakeCol(3, "Income") };
        var date = new DateOnly(2026, 1, 1);
        var rows = new[]
        {
            new RawImportRow(date, new Dictionary<int, decimal> { [1] = 0m }, 0m)
        };
        SetupParser(cols, rows);

        var result = await CreateSut().ExecuteAsync(UserId, MakeMapping([1]));

        Assert.Equal(0, result.ExpensesUpserted);
        _expenseRepo.Verify(r => r.Add(It.IsAny<DailyExpense>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ZeroIncomeAmount_SkipsUpsert()
    {
        var cols = new[] { MakeCol(0, "Date"), MakeCol(1, "Groceries"), MakeCol(3, "Income") };
        var date = new DateOnly(2026, 1, 1);
        var rows = new[]
        {
            new RawImportRow(date, new Dictionary<int, decimal> { [1] = 50m }, 0m)
        };
        SetupParser(cols, rows);

        await CreateSut().ExecuteAsync(UserId, MakeMapping([1]));

        _incomeRepo.Verify(r => r.Add(It.IsAny<DailyIncome>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ExpenseAmount_FlowsThroughToUpsert()
    {
        var cols = new[] { MakeCol(0, "Date"), MakeCol(1, "Groceries"), MakeCol(3, "Income") };
        var date = new DateOnly(2026, 1, 1);
        var rows = new[]
        {
            new RawImportRow(date, new Dictionary<int, decimal> { [1] = 350m }, 0m)
        };
        SetupParser(cols, rows);

        await CreateSut().ExecuteAsync(UserId, MakeMapping([1]));

        var added = Assert.Single(_expenseData);
        Assert.Equal(350m, added.Amount);
    }

    [Fact]
    public async Task Execute_SkippedRows_AreCountedInResult()
    {
        var cols = new[] { MakeCol(0, "Date"), MakeCol(1, "Groceries"), MakeCol(3, "Income") };
        SetupParser(cols, Array.Empty<RawImportRow>(), skippedRows: 2);

        var result = await CreateSut().ExecuteAsync(UserId, MakeMapping([1]));

        Assert.Equal(2, result.RowsSkipped);
    }

    [Fact]
    public async Task Execute_OnSuccess_DeletesTempFile()
    {
        var cols = new[] { MakeCol(0, "Date"), MakeCol(1, "Groceries"), MakeCol(3, "Income") };
        SetupParser(cols, Array.Empty<RawImportRow>());

        await CreateSut().ExecuteAsync(UserId, MakeMapping([1]));

        _store.Verify(s => s.Delete(FileId), Times.Once);
    }

    [Fact]
    public async Task Execute_MissingFile_ThrowsNotFoundException()
    {
        _store.Setup(s => s.Exists(It.IsAny<string>())).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateSut().ExecuteAsync(UserId, MakeMapping([1])));
    }
}
