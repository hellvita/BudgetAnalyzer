using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Domain.Entities;
using BudgetAnalyzer.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BudgetAnalyzer.Application.Categories;

public class CategoryService
{
    private readonly IRepository<Category> _categories;
    private readonly IUnitOfWork _uow;

    public CategoryService(IRepository<Category> categories, IUnitOfWork uow)
    {
        _categories = categories;
        _uow = uow;
    }

    public async Task<List<CategoryResponse>> ListAsync(
        Guid userId,
        bool includeArchived = false,
        CancellationToken ct = default)
    {
        var query = _categories.Query().Where(c => c.UserId == userId);
        if (!includeArchived)
            query = query.Where(c => !c.IsArchived);

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse(c.Id, c.Name, c.IsArchived))
            .ToListAsync(ct);
    }

    public async Task<CategoryResponse> CreateAsync(Guid userId, string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        await ThrowIfNameConflict(userId, trimmed, excludeId: null, ct);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = trimmed,
            IsArchived = false,
        };

        _categories.Add(category);
        await _uow.SaveChangesAsync(ct);

        return new CategoryResponse(category.Id, category.Name, category.IsArchived);
    }

    public async Task<CategoryResponse> RenameAsync(Guid userId, Guid id, string name, CancellationToken ct = default)
    {
        var category = await GetOwnedAsync(userId, id, ct);
        var trimmed = name.Trim();
        await ThrowIfNameConflict(userId, trimmed, excludeId: id, ct);

        category.Name = trimmed;
        _categories.Update(category);
        await _uow.SaveChangesAsync(ct);

        return new CategoryResponse(category.Id, category.Name, category.IsArchived);
    }

    public async Task ArchiveAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var category = await GetOwnedAsync(userId, id, ct);
        category.IsArchived = true;
        _categories.Update(category);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task UnarchiveAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var category = await GetOwnedAsync(userId, id, ct);
        await ThrowIfNameConflict(userId, category.Name, excludeId: id, ct);

        category.IsArchived = false;
        _categories.Update(category);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Category> GetOwnedAsync(Guid userId, Guid id, CancellationToken ct)
    {
        return await _categories.Query()
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct)
            ?? throw new NotFoundException($"Category {id} not found.");
    }

    public async Task<(Guid id, bool wasCreated)> GetOrCreateAsync(
        Guid userId, string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        var existing = await _categories.Query()
            .Where(c => c.UserId == userId && !c.IsArchived && c.Name.ToLower() == trimmed.ToLower())
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

        if (existing.HasValue)
            return (existing.Value, wasCreated: false);

        var category = new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = trimmed,
            IsArchived = false,
        };
        _categories.Add(category);
        await _uow.SaveChangesAsync(ct);
        return (category.Id, wasCreated: true);
    }

    private async Task ThrowIfNameConflict(Guid userId, string name, Guid? excludeId, CancellationToken ct)
    {
        var conflict = await _categories.Query()
            .AnyAsync(c =>
                c.UserId == userId &&
                !c.IsArchived &&
                c.Name == name &&
                c.Id != (excludeId ?? Guid.Empty),
                ct);

        if (conflict)
            throw new ConflictException($"An active category named '{name}' already exists.");
    }
}
