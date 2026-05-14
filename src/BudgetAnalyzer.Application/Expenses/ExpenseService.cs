using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Domain.Entities;
using BudgetAnalyzer.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BudgetAnalyzer.Application.Expenses;

public class ExpenseService
{
    private readonly IRepository<DailyExpense> _expenses;
    private readonly IRepository<Category> _categories;
    private readonly IUnitOfWork _uow;

    public ExpenseService(
        IRepository<DailyExpense> expenses,
        IRepository<Category> categories,
        IUnitOfWork uow)
    {
        _expenses = expenses;
        _categories = categories;
        _uow = uow;
    }

    public async Task UpsertAsync(
        Guid userId,
        Guid categoryId,
        DateOnly date,
        decimal amount,
        CancellationToken ct = default)
    {
        if (amount < 0)
            throw new ValidationException("Amount must be 0 or greater.");

        var category = await _categories.Query()
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, ct)
            ?? throw new NotFoundException($"Category {categoryId} not found.");

        if (category.IsArchived)
            throw new ValidationException($"Category '{category.Name}' is archived and cannot receive new expenses.");

        var existing = await _expenses.Query()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.CategoryId == categoryId && e.Date == date, ct);

        if (existing is not null)
        {
            existing.Amount = amount;
            _expenses.Update(existing);
        }
        else
        {
            _expenses.Add(new DailyExpense
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CategoryId = categoryId,
                Date = date,
                Amount = amount,
            });
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(
        Guid userId,
        Guid categoryId,
        DateOnly date,
        CancellationToken ct = default)
    {
        var expense = await _expenses.Query()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.CategoryId == categoryId && e.Date == date, ct)
            ?? throw new NotFoundException($"No expense found for category {categoryId} on {date}.");

        _expenses.Remove(expense);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<ExpenseByDateResponse> GetByDateAsync(
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

        var perCategory = activeCategories
            .Select(c => new ExpenseByCategoryItem(
                c.Id,
                c.Name,
                expenseMap.TryGetValue(c.Id, out var a) ? a : 0m))
            .ToList();

        return new ExpenseByDateResponse(date, perCategory, perCategory.Sum(x => x.Amount));
    }

    public async Task<List<ExpenseByMonthDayItem>> GetByMonthAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken ct = default)
    {
        var firstDay = new DateOnly(year, month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        var activeCategories = await _categories.Query()
            .Where(c => c.UserId == userId && !c.IsArchived)
            .OrderBy(c => c.Name)
            .Select(c => c.Id)
            .ToListAsync(ct);

        var monthExpenses = await _expenses.Query()
            .Where(e => e.UserId == userId && e.Date >= firstDay && e.Date <= lastDay)
            .Select(e => new { e.CategoryId, e.Date, e.Amount })
            .ToListAsync(ct);

        var expenseMap = monthExpenses.ToDictionary(e => (e.Date, e.CategoryId), e => e.Amount);

        var days = new List<ExpenseByMonthDayItem>();
        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            var perCategory = activeCategories
                .Select(id => new ExpenseMonthPerCategory(
                    id,
                    expenseMap.TryGetValue((day, id), out var a) ? a : 0m))
                .ToList();

            days.Add(new ExpenseByMonthDayItem(day, perCategory, perCategory.Sum(x => x.Amount)));
        }

        return days;
    }
}
