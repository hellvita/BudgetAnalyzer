using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Domain.Entities;
using BudgetAnalyzer.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BudgetAnalyzer.Application.Users;

public class UserService
{
    private readonly IRepository<User> _users;
    private readonly IRepository<Category> _categories;
    private readonly IRepository<DailyExpense> _expenses;
    private readonly IRepository<DailyIncome> _incomes;
    private readonly IRepository<DailyLimit> _limits;
    private readonly IUnitOfWork _uow;

    public UserService(
        IRepository<User> users,
        IRepository<Category> categories,
        IRepository<DailyExpense> expenses,
        IRepository<DailyIncome> incomes,
        IRepository<DailyLimit> limits,
        IUnitOfWork uow)
    {
        _users = users;
        _categories = categories;
        _expenses = expenses;
        _incomes = incomes;
        _limits = limits;
        _uow = uow;
    }

    public async Task DeleteAccountAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException($"User {userId} not found.");

        // RESTRICT FK: expenses must be removed before categories can be removed
        var expenses = await _expenses.Query().Where(e => e.UserId == userId).ToListAsync(ct);
        _expenses.RemoveRange(expenses);

        var incomes = await _incomes.Query().Where(i => i.UserId == userId).ToListAsync(ct);
        _incomes.RemoveRange(incomes);

        var limits = await _limits.Query().Where(l => l.UserId == userId).ToListAsync(ct);
        _limits.RemoveRange(limits);

        var categories = await _categories.Query().Where(c => c.UserId == userId).ToListAsync(ct);
        _categories.RemoveRange(categories);

        _users.Remove(user);
        await _uow.SaveChangesAsync(ct);
    }
}
