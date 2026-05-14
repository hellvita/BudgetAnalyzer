using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Domain.Entities;
using BudgetAnalyzer.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BudgetAnalyzer.Application.Budget;

public class BudgetService
{
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;

    public BudgetService(IRepository<User> users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<decimal> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.Query()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.");

        return user.InitialBudget;
    }

    public async Task SetAsync(Guid userId, decimal newValue, CancellationToken ct = default)
    {
        if (newValue < 0)
            throw new ValidationException("Initial budget must be 0 or greater.");

        var user = await _users.Query()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("User not found.");

        user.InitialBudget = newValue;
        _users.Update(user);
        await _uow.SaveChangesAsync(ct);
    }
}
