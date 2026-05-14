using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Domain.Entities;
using BudgetAnalyzer.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BudgetAnalyzer.Application.Incomes;

public class IncomeService
{
    private readonly IRepository<DailyIncome> _incomes;
    private readonly IUnitOfWork _uow;

    public IncomeService(IRepository<DailyIncome> incomes, IUnitOfWork uow)
    {
        _incomes = incomes;
        _uow = uow;
    }

    public async Task UpsertAsync(
        Guid userId,
        DateOnly date,
        decimal amount,
        CancellationToken ct = default)
    {
        if (amount < 0)
            throw new ValidationException("Amount must be 0 or greater.");

        var existing = await _incomes.Query()
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Date == date, ct);

        if (existing is not null)
        {
            existing.Amount = amount;
            _incomes.Update(existing);
        }
        else
        {
            _incomes.Add(new DailyIncome
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Date = date,
                Amount = amount,
            });
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(
        Guid userId,
        DateOnly date,
        CancellationToken ct = default)
    {
        var income = await _incomes.Query()
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Date == date, ct)
            ?? throw new NotFoundException($"No income found for {date}.");

        _incomes.Remove(income);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<decimal> GetByDateAsync(
        Guid userId,
        DateOnly date,
        CancellationToken ct = default)
    {
        var income = await _incomes.Query()
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Date == date, ct);

        return income?.Amount ?? 0m;
    }

    public async Task<List<IncomeByMonthDayItem>> GetByMonthAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken ct = default)
    {
        var firstDay = new DateOnly(year, month, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);

        var monthIncomes = await _incomes.Query()
            .Where(i => i.UserId == userId && i.Date >= firstDay && i.Date <= lastDay)
            .Select(i => new { i.Date, i.Amount })
            .ToDictionaryAsync(i => i.Date, i => i.Amount, ct);

        var days = new List<IncomeByMonthDayItem>();
        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            days.Add(new IncomeByMonthDayItem(
                day,
                monthIncomes.TryGetValue(day, out var a) ? a : 0m));
        }

        return days;
    }
}
