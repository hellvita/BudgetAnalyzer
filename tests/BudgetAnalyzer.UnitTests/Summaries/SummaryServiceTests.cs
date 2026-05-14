using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Summaries;
using BudgetAnalyzer.Domain.Entities;
using BudgetAnalyzer.UnitTests.Infrastructure;
using Moq;

namespace BudgetAnalyzer.UnitTests.Summaries;

public class SummaryServiceTests
{
    private readonly Mock<IRepository<DailyExpense>> _expenseRepo = new();
    private readonly Mock<IRepository<DailyIncome>> _incomeRepo = new();
    private readonly Mock<IRepository<DailyLimit>> _limitRepo = new();
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IRepository<Category>> _categoryRepo = new();

    private SummaryService CreateSut() => new(
        _expenseRepo.Object,
        _incomeRepo.Object,
        _limitRepo.Object,
        _userRepo.Object,
        _categoryRepo.Object);

    private static readonly Guid UserId = Guid.NewGuid();

    private void SetupCategories(params (Guid Id, string Name, bool IsArchived)[] cats)
    {
        var list = cats.Select(c => new Category
        {
            Id = c.Id,
            UserId = UserId,
            Name = c.Name,
            IsArchived = c.IsArchived,
        }).ToList();
        _categoryRepo.Setup(r => r.Query()).Returns(list.AsAsyncQueryable());
    }

    private void SetupExpenses(params (Guid CategoryId, DateOnly Date, decimal Amount)[] items)
    {
        var list = items.Select(i => new DailyExpense
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            CategoryId = i.CategoryId,
            Date = i.Date,
            Amount = i.Amount,
        }).ToList();
        _expenseRepo.Setup(r => r.Query()).Returns(list.AsAsyncQueryable());
    }

    private void SetupIncomes(params (DateOnly Date, decimal Amount)[] items)
    {
        var list = items.Select(i => new DailyIncome
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Date = i.Date,
            Amount = i.Amount,
        }).ToList();
        _incomeRepo.Setup(r => r.Query()).Returns(list.AsAsyncQueryable());
    }

    private void SetupLimits(params (DateOnly EffectiveFromDate, decimal Amount)[] items)
    {
        var list = items.Select(i => new DailyLimit
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            EffectiveFromDate = i.EffectiveFromDate,
            Amount = i.Amount,
        }).ToList();
        _limitRepo.Setup(r => r.Query()).Returns(list.AsAsyncQueryable());
    }

    private void SetupUser(decimal initialBudget = 0m)
    {
        var list = new List<User>
        {
            new() { Id = UserId, Email = "unit@tests.budget.dev", PasswordHash = "hash", InitialBudget = initialBudget }
        };
        _userRepo.Setup(r => r.Query()).Returns(list.AsAsyncQueryable());
    }

    // ---- GetDayAsync tests ----

    [Fact]
    public async Task GetDay_NoData_ReturnsAllZerosNullLimit()
    {
        SetupCategories();
        SetupExpenses();
        SetupIncomes();
        SetupLimits();

        var sut = CreateSut();
        var result = await sut.GetDayAsync(UserId, new DateOnly(2026, 1, 1));

        Assert.Equal(0m, result.Income);
        Assert.Equal(0m, result.TotalExpenses);
        Assert.Null(result.EffectiveLimit);
        Assert.Null(result.LimitDiff);
        Assert.Equal(0m, result.Net);
    }

    [Fact]
    public async Task GetDay_WithExpenseAndIncome_ReturnsCorrectValues()
    {
        var catId = Guid.NewGuid();
        var date = new DateOnly(2026, 6, 15);
        SetupCategories((catId, "Food", false));
        SetupExpenses((catId, date, 120m));
        SetupIncomes((date, 500m));
        SetupLimits((new DateOnly(2026, 6, 1), 200m));

        var result = await CreateSut().GetDayAsync(UserId, date);

        Assert.Equal(500m, result.Income);
        Assert.Equal(120m, result.TotalExpenses);
        Assert.Equal(200m, result.EffectiveLimit);
        Assert.Equal(80m, result.LimitDiff);  // 200 - 120
        Assert.Equal(380m, result.Net);       // 500 - 120
    }

    [Fact]
    public async Task GetDay_NoLimitSet_LimitDiffIsNull()
    {
        var catId = Guid.NewGuid();
        var date = new DateOnly(2026, 9, 10);
        SetupCategories((catId, "Bills", false));
        SetupExpenses((catId, date, 50m));
        SetupIncomes();
        SetupLimits();

        var result = await CreateSut().GetDayAsync(UserId, date);

        Assert.Null(result.EffectiveLimit);
        Assert.Null(result.LimitDiff);
        Assert.Equal(50m, result.TotalExpenses);
    }

    [Fact]
    public async Task GetDay_LimitBeforeDate_LimitDiffIsNull()
    {
        var date = new DateOnly(2026, 5, 1);
        // Limit is effective from May 2 — before May 1, no limit applies
        SetupCategories();
        SetupExpenses();
        SetupIncomes();
        SetupLimits((new DateOnly(2026, 5, 2), 100m));

        var result = await CreateSut().GetDayAsync(UserId, date);

        Assert.Null(result.EffectiveLimit);
        Assert.Null(result.LimitDiff);
    }

    [Fact]
    public async Task GetDay_ActiveCategoryWithNoExpense_ShowsZeroAmount()
    {
        var catId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 20);
        SetupCategories((catId, "Transport", false));
        SetupExpenses(); // no entries
        SetupIncomes();
        SetupLimits();

        var result = await CreateSut().GetDayAsync(UserId, date);

        Assert.Single(result.ExpensesByCategory);
        Assert.Equal(0m, result.ExpensesByCategory[0].Amount);
        Assert.Equal(catId, result.ExpensesByCategory[0].CategoryId);
    }

    [Fact]
    public async Task GetDay_ArchivedCategoryWithNoCurrentExpense_NotInList()
    {
        var archivedCatId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 1);
        SetupCategories((archivedCatId, "OldCat", true));
        SetupExpenses(); // no entries for this date
        SetupIncomes();
        SetupLimits();

        var result = await CreateSut().GetDayAsync(UserId, date);

        Assert.DoesNotContain(result.ExpensesByCategory, c => c.CategoryId == archivedCatId);
    }

    [Fact]
    public async Task GetDay_ArchivedCategoryWithHistoricalExpense_AppearsInList()
    {
        var archivedCatId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 5);
        SetupCategories((archivedCatId, "OldCat", true));
        SetupExpenses((archivedCatId, date, 75m));
        SetupIncomes();
        SetupLimits();

        var result = await CreateSut().GetDayAsync(UserId, date);

        Assert.Contains(result.ExpensesByCategory, c => c.CategoryId == archivedCatId && c.Amount == 75m);
    }

    // ---- GetMonthAsync tests ----

    [Fact]
    public async Task GetMonth_NoData_ReturnsCorrectDayCountAllZeros()
    {
        SetupCategories();
        SetupExpenses();
        SetupIncomes();
        SetupLimits();

        var result = await CreateSut().GetMonthAsync(UserId, 2026, 2); // February

        Assert.Equal(28, result.Days.Count); // 2026 is not a leap year
        Assert.All(result.Days, d =>
        {
            Assert.Equal(0m, d.TotalExpenses);
            Assert.Equal(0m, d.TotalIncome);
            Assert.Null(d.EffectiveLimit);
        });
        Assert.Equal(0m, result.MonthTotals.TotalExpenses);
        Assert.Equal(0m, result.MonthTotals.TotalIncome);
    }

    [Fact]
    public async Task GetMonth_LimitChangesMidMonth_AllowedBudgetIsCorrect()
    {
        // November has 30 days. Limit: first 14 days at 100, days 15-30 at 200
        SetupCategories();
        SetupExpenses();
        SetupIncomes();
        SetupLimits(
            (new DateOnly(2026, 11, 1), 100m),
            (new DateOnly(2026, 11, 15), 200m));

        var result = await CreateSut().GetMonthAsync(UserId, 2026, 11);

        // Nov 1-14 = 14 days at 100, Nov 15-30 = 16 days at 200
        var expected = 14 * 100m + 16 * 200m;
        Assert.Equal(expected, result.MonthTotals.AllowedMonthlyBudget);
    }

    [Fact]
    public async Task GetMonth_WithExpensesAndIncome_TotalsAreCorrect()
    {
        var catId = Guid.NewGuid();
        SetupCategories((catId, "Rent", false));
        SetupExpenses(
            (catId, new DateOnly(2026, 10, 5), 800m),
            (catId, new DateOnly(2026, 10, 20), 200m));
        SetupIncomes((new DateOnly(2026, 10, 1), 3000m));
        SetupLimits((new DateOnly(2026, 10, 1), 50m));

        var result = await CreateSut().GetMonthAsync(UserId, 2026, 10);

        Assert.Equal(1000m, result.MonthTotals.TotalExpenses);
        Assert.Equal(3000m, result.MonthTotals.TotalIncome);
        Assert.Equal(2000m, result.MonthTotals.Net);
        Assert.Equal(31 * 50m, result.MonthTotals.AllowedMonthlyBudget); // 1550
    }

    [Fact]
    public async Task GetMonth_NoLimitSet_AllowedBudgetIsZero()
    {
        SetupCategories();
        SetupExpenses();
        SetupIncomes();
        SetupLimits(); // no limits

        var result = await CreateSut().GetMonthAsync(UserId, 2026, 3);

        Assert.Equal(0m, result.MonthTotals.AllowedMonthlyBudget);
        Assert.Equal(0m, result.MonthTotals.TotalLimitDiff);
    }

    // ---- GetAllTimeAsync tests ----

    [Fact]
    public async Task GetAllTime_EmptyAccount_ReturnsAllZeros()
    {
        SetupUser(0m);
        SetupExpenses();
        SetupIncomes();
        SetupLimits();

        var result = await CreateSut().GetAllTimeAsync(UserId);

        Assert.Equal(0m, result.InitialBudget);
        Assert.Equal(0m, result.TotalIncome);
        Assert.Equal(0m, result.TotalExpenses);
        Assert.Equal(0m, result.CurrentBalance);
        Assert.Equal(0m, result.Net);
    }

    [Fact]
    public async Task GetAllTime_Balance_IsInitialPlusIncomMinusExpenses()
    {
        SetupUser(1000m);
        var catId = Guid.NewGuid();
        SetupExpenses((catId, new DateOnly(2026, 1, 1), 400m));
        SetupIncomes((new DateOnly(2026, 1, 1), 2000m));
        SetupLimits();

        var result = await CreateSut().GetAllTimeAsync(UserId);

        Assert.Equal(1000m, result.InitialBudget);
        Assert.Equal(2000m, result.TotalIncome);
        Assert.Equal(400m, result.TotalExpenses);
        Assert.Equal(2600m, result.CurrentBalance); // 1000 + 2000 - 400
        Assert.Equal(1600m, result.Net);            // 2000 - 400
    }

    [Fact]
    public async Task GetAllTime_NegativeBalance_AllowedAndCorrect()
    {
        SetupUser(0m);
        var catId = Guid.NewGuid();
        SetupExpenses((catId, new DateOnly(2026, 1, 1), 500m));
        SetupIncomes((new DateOnly(2026, 1, 1), 100m));
        SetupLimits();

        var result = await CreateSut().GetAllTimeAsync(UserId);

        Assert.Equal(-400m, result.CurrentBalance); // 0 + 100 - 500
        Assert.Equal(-400m, result.Net);
    }

    [Fact]
    public async Task GetAllTime_TotalLimitDiff_OnlyIncludesActivityDates()
    {
        SetupUser(0m);
        var catId = Guid.NewGuid();
        // Activity on Jan 1 and Jan 3. Limit = 100 from Jan 1.
        SetupExpenses(
            (catId, new DateOnly(2026, 1, 1), 60m),
            (catId, new DateOnly(2026, 1, 3), 40m));
        SetupIncomes(); // no income — but expense dates are still activity dates
        SetupLimits((new DateOnly(2026, 1, 1), 100m));

        var result = await CreateSut().GetAllTimeAsync(UserId);

        // Jan 1: diff = 100 - 60 = 40
        // Jan 3: diff = 100 - 40 = 60
        // Total = 100
        Assert.Equal(100m, result.TotalLimitDiff);
    }

    [Fact]
    public async Task GetAllTime_ActivityDateWithNoLimit_SkippedInTotalLimitDiff()
    {
        SetupUser(0m);
        var catId = Guid.NewGuid();
        // Expense before any limit entry
        SetupExpenses((catId, new DateOnly(2026, 1, 1), 50m));
        SetupIncomes();
        SetupLimits((new DateOnly(2026, 1, 5), 100m)); // limit starts Jan 5, not Jan 1

        var result = await CreateSut().GetAllTimeAsync(UserId);

        // Jan 1 has no effective limit → skipped
        Assert.Equal(0m, result.TotalLimitDiff);
    }
}
