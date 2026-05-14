using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Domain.Entities;
using BudgetAnalyzer.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BudgetAnalyzer.Application.Limits;

public class LimitService
{
    private readonly IRepository<DailyLimit> _limits;
    private readonly IUnitOfWork _uow;

    public LimitService(IRepository<DailyLimit> limits, IUnitOfWork uow)
    {
        _limits = limits;
        _uow = uow;
    }

    public async Task SetAsync(
        Guid userId,
        DateOnly effectiveFrom,
        decimal amount,
        CancellationToken ct = default)
    {
        if (amount < 0)
            throw new ValidationException("Amount must be 0 or greater.");

        var existing = await _limits.Query()
            .FirstOrDefaultAsync(l => l.UserId == userId && l.EffectiveFromDate == effectiveFrom, ct);

        if (existing is not null)
        {
            existing.Amount = amount;
            _limits.Update(existing);
        }
        else
        {
            _limits.Add(new DailyLimit
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EffectiveFromDate = effectiveFrom,
                Amount = amount,
            });
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<decimal?> GetEffectiveAsync(
        Guid userId,
        DateOnly date,
        CancellationToken ct = default)
    {
        var limit = await _limits.Query()
            .Where(l => l.UserId == userId && l.EffectiveFromDate <= date)
            .OrderByDescending(l => l.EffectiveFromDate)
            .Select(l => (decimal?)l.Amount)
            .FirstOrDefaultAsync(ct);

        return limit;
    }

    public async Task<List<LimitHistoryItem>> GetHistoryAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _limits.Query()
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.EffectiveFromDate)
            .Select(l => new LimitHistoryItem(l.EffectiveFromDate, l.Amount))
            .ToListAsync(ct);
    }

    public async Task DeleteAsync(
        Guid userId,
        DateOnly effectiveFrom,
        CancellationToken ct = default)
    {
        var limit = await _limits.Query()
            .FirstOrDefaultAsync(l => l.UserId == userId && l.EffectiveFromDate == effectiveFrom, ct)
            ?? throw new NotFoundException($"No limit found for effective date {effectiveFrom}.");

        _limits.Remove(limit);
        await _uow.SaveChangesAsync(ct);
    }
}
