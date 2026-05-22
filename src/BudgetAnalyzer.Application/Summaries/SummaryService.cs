using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Domain.Entities;
using BudgetAnalyzer.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BudgetAnalyzer.Application.Summaries;

public class SummaryService
{
    private readonly IRepository<DailyExpense> _expenses;
    private readonly IRepository<DailyIncome> _incomes;
    private readonly IRepository<DailyLimit> _limits;
    private readonly IRepository<User> _users;
    private readonly IRepository<Category> _categories;

    public SummaryService(
        IRepository<DailyExpense> expenses,
        IRepository<DailyIncome> incomes,
        IRepository<DailyLimit> limits,
        IRepository<User> users,
        IRepository<Category> categories)
    {
        _expenses = expenses;
        _incomes = incomes;
        _limits = limits;
        _users = users;
        _categories = categories;
    }

    public async Task<DaySummaryResponse> GetDayAsync(
        Guid userId,
        DateOnly date,
        CancellationToken ct = default)
    {
        var activeCategories = await _categories.Query()
            .Where(c => c.UserId == userId && !c.IsArchived)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);

        var expenseMap = await _expenses.Query()
            .Where(e => e.UserId == userId && e.Date == date)
            .Select(e => new { e.CategoryId, e.Amount })
            .ToDictionaryAsync(e => e.CategoryId, e => e.Amount, ct);

        // Archived categories that have actual entries on this day also appear
        var activeCategoryIds = activeCategories.Select(c => c.Id).ToHashSet();
        var archivedWithEntries = expenseMap.Keys.Except(activeCategoryIds).ToList();
        var archivedNames = archivedWithEntries.Count > 0
            ? await _categories.Query()
                .Where(c => archivedWithEntries.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct)
            : new Dictionary<Guid, string>();

        var perCategory = activeCategories
            .Select(c => new SummaryExpenseByCategory(
                c.Id, c.Name,
                expenseMap.TryGetValue(c.Id, out var a) ? a : 0m))
            .Concat(archivedWithEntries
                .Select(id => new SummaryExpenseByCategory(
                    id,
                    archivedNames.GetValueOrDefault(id, "Unknown"),
                    expenseMap[id])))
            .ToList();

        var totalExpenses = perCategory.Sum(x => x.Amount);

        var income = await _incomes.Query()
            .Where(i => i.UserId == userId && i.Date == date)
            .Select(i => (decimal?)i.Amount)
            .FirstOrDefaultAsync(ct) ?? 0m;

        var effectiveLimit = await _limits.Query()
            .Where(l => l.UserId == userId && l.EffectiveFromDate <= date)
            .OrderByDescending(l => l.EffectiveFromDate)
            .Select(l => (decimal?)l.Amount)
            .FirstOrDefaultAsync(ct);

        var limitDiff = effectiveLimit.HasValue ? effectiveLimit.Value - totalExpenses : (decimal?)null;

        return new DaySummaryResponse(
            date,
            income,
            perCategory,
            totalExpenses,
            effectiveLimit,
            limitDiff,
            income - totalExpenses);
    }

    public async Task<MonthSummaryResponse> GetMonthAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken ct = default)
    {
        var firstDay = new DateOnly(year, month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        // Opening balance: initial budget + all income before this month − all expenses before this month
        var initialBudget = await _users.Query()
            .Where(u => u.Id == userId)
            .Select(u => u.InitialBudget)
            .FirstAsync(ct);

        var priorIncome = await _incomes.Query()
            .Where(i => i.UserId == userId && i.Date < firstDay)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;

        var priorExpenses = await _expenses.Query()
            .Where(e => e.UserId == userId && e.Date < firstDay)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var openingBalance = initialBudget + priorIncome - priorExpenses;

        var activeCategories = await _categories.Query()
            .Where(c => c.UserId == userId && !c.IsArchived)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);

        var monthExpenseRows = await _expenses.Query()
            .Where(e => e.UserId == userId && e.Date >= firstDay && e.Date <= lastDay)
            .Select(e => new { e.Date, e.CategoryId, e.Amount })
            .ToListAsync(ct);

        var monthIncomeRows = await _incomes.Query()
            .Where(i => i.UserId == userId && i.Date >= firstDay && i.Date <= lastDay)
            .Select(i => new { i.Date, i.Amount })
            .ToListAsync(ct);

        // All limit entries on or before the end of the month (sorted asc — used by GetEffectiveLimit)
        var limitEntries = await _limits.Query()
            .Where(l => l.UserId == userId && l.EffectiveFromDate <= lastDay)
            .OrderBy(l => l.EffectiveFromDate)
            .Select(l => new { l.EffectiveFromDate, l.Amount })
            .ToListAsync(ct);

        var limitTuples = limitEntries
            .Select(l => (l.EffectiveFromDate, l.Amount))
            .ToList();

        // Resolve names for all categories that appear in month expenses (active + archived)
        var activeCategoryIds = activeCategories.Select(c => c.Id).ToHashSet();
        var expenseByCategoryInMonth = monthExpenseRows
            .GroupBy(e => e.CategoryId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        var archivedWithEntries = expenseByCategoryInMonth.Keys.Except(activeCategoryIds).ToList();
        var archivedNames = archivedWithEntries.Count > 0
            ? await _categories.Query()
                .Where(c => archivedWithEntries.Contains(c.Id))
                .Select(c => new { c.Id, c.Name })
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct)
            : new Dictionary<Guid, string>();

        var categoryNameById = activeCategories
            .ToDictionary(c => c.Id, c => c.Name)
            .Concat(archivedNames)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        // Per-day per-category breakdown (only days/categories with actual entries)
        var expensesByDayAndCategory = monthExpenseRows
            .GroupBy(e => e.Date)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SummaryExpenseByCategory>)g
                    .GroupBy(e => e.CategoryId)
                    .Select(cg => new SummaryExpenseByCategory(
                        cg.Key,
                        categoryNameById.GetValueOrDefault(cg.Key, "Unknown"),
                        cg.Sum(e => e.Amount)))
                    .ToList());

        var incomeByDay = monthIncomeRows.ToDictionary(i => i.Date, i => i.Amount);

        var days = new List<MonthSummaryDayItem>();
        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            var dayExpensesByCategory = expensesByDayAndCategory.TryGetValue(day, out var dayCats)
                ? dayCats
                : Array.Empty<SummaryExpenseByCategory>();
            var dayExpenses = dayExpensesByCategory.Sum(e => e.Amount);
            var dayIncome = incomeByDay.TryGetValue(day, out var di) ? di : 0m;
            var effectiveLimit = GetEffectiveLimit(limitTuples, day);
            var limitDiff = effectiveLimit.HasValue ? effectiveLimit.Value - dayExpenses : (decimal?)null;

            days.Add(new MonthSummaryDayItem(
                day, dayExpenses, dayIncome, effectiveLimit, limitDiff,
                dayIncome - dayExpenses, dayExpensesByCategory));
        }

        // Month-level totals: active categories (including zeros) + archived ones with entries
        var monthExpensesByCategory = activeCategories
            .Select(c => new SummaryExpenseByCategory(
                c.Id, c.Name,
                expenseByCategoryInMonth.TryGetValue(c.Id, out var amt) ? amt : 0m))
            .Concat(archivedWithEntries
                .Select(id => new SummaryExpenseByCategory(
                    id,
                    archivedNames.GetValueOrDefault(id, "Unknown"),
                    expenseByCategoryInMonth[id])))
            .ToList();

        var totalExpenses = days.Sum(d => d.TotalExpenses);
        var totalIncome = days.Sum(d => d.TotalIncome);
        var allowedMonthlyBudget = days.Where(d => d.EffectiveLimit.HasValue).Sum(d => d.EffectiveLimit!.Value);
        var totalLimitDiff = days.Where(d => d.LimitDiff.HasValue).Sum(d => d.LimitDiff!.Value);

        var monthTotals = new MonthTotals(
            totalExpenses,
            totalIncome,
            monthExpensesByCategory,
            allowedMonthlyBudget,
            totalLimitDiff,
            totalIncome - totalExpenses);

        return new MonthSummaryResponse(year, month, openingBalance, days, monthTotals);
    }

    public async Task<IReadOnlyList<(int Year, int Month)>> GetMonthsWithDataAsync(
        Guid userId, CancellationToken ct = default)
    {
        var expenseMonths = await _expenses.Query()
            .Where(e => e.UserId == userId)
            .Select(e => new { e.Date.Year, e.Date.Month })
            .Distinct()
            .ToListAsync(ct);

        var incomeMonths = await _incomes.Query()
            .Where(i => i.UserId == userId)
            .Select(i => new { i.Date.Year, i.Date.Month })
            .Distinct()
            .ToListAsync(ct);

        return expenseMonths
            .UnionBy(incomeMonths, x => (x.Year, x.Month))
            .Select(x => (x.Year, x.Month))
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();
    }

    public async Task<IReadOnlyList<MonthSummaryResponse>> GetAllTimeMonthlyAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var months = await GetMonthsWithDataAsync(userId, ct);
        var results = new List<MonthSummaryResponse>(months.Count);
        foreach (var (year, month) in months)
            results.Add(await GetMonthAsync(userId, year, month, ct));
        return results;
    }

    public async Task<AllTimeSummaryResponse> GetAllTimeAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var user = await _users.Query()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.");

        var totalIncome = await _incomes.Query()
            .Where(i => i.UserId == userId)
            .SumAsync(i => (decimal?)i.Amount, ct) ?? 0m;

        var totalExpenses = await _expenses.Query()
            .Where(e => e.UserId == userId)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        // Per-date expense sums (aggregated in SQL)
        var expenseSumByDate = await _expenses.Query()
            .Where(e => e.UserId == userId)
            .GroupBy(e => e.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync(ct);

        var incomeDates = await _incomes.Query()
            .Where(i => i.UserId == userId)
            .Select(i => i.Date)
            .ToListAsync(ct);

        var expenseByDateMap = expenseSumByDate.ToDictionary(x => x.Date, x => x.Total);

        // All dates with any activity (expense or income)
        var allActivityDates = expenseByDateMap.Keys.Union(incomeDates).ToList();

        var limitEntries = await _limits.Query()
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.EffectiveFromDate)
            .Select(l => new { l.EffectiveFromDate, l.Amount })
            .ToListAsync(ct);

        var limitTuples = limitEntries
            .Select(l => (l.EffectiveFromDate, l.Amount))
            .ToList();

        decimal totalLimitDiff = 0m;
        foreach (var date in allActivityDates)
        {
            var effectiveLimit = GetEffectiveLimit(limitTuples, date);
            if (!effectiveLimit.HasValue)
                continue;

            var dayExpenses = expenseByDateMap.TryGetValue(date, out var de) ? de : 0m;
            totalLimitDiff += effectiveLimit.Value - dayExpenses;
        }

        var currentBalance = user.InitialBudget + totalIncome - totalExpenses;

        return new AllTimeSummaryResponse(
            user.InitialBudget,
            totalIncome,
            totalExpenses,
            totalLimitDiff,
            currentBalance,
            totalIncome - totalExpenses);
    }

    // Limits must be sorted ascending by EffectiveFromDate
    private static decimal? GetEffectiveLimit(List<(DateOnly Date, decimal Amount)> limits, DateOnly day)
    {
        decimal? result = null;
        foreach (var (date, amount) in limits)
        {
            if (date <= day)
                result = amount;
            else
                break;
        }
        return result;
    }
}
