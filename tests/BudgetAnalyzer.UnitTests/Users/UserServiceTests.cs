using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Application.Users;
using BudgetAnalyzer.Domain.Entities;
using BudgetAnalyzer.Domain.Exceptions;
using BudgetAnalyzer.UnitTests.Infrastructure;
using Moq;

namespace BudgetAnalyzer.UnitTests.Users;

public class UserServiceTests
{
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IRepository<Category>> _categoryRepo = new();
    private readonly Mock<IRepository<DailyExpense>> _expenseRepo = new();
    private readonly Mock<IRepository<DailyIncome>> _incomeRepo = new();
    private readonly Mock<IRepository<DailyLimit>> _limitRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private UserService CreateSut() => new(
        _userRepo.Object,
        _categoryRepo.Object,
        _expenseRepo.Object,
        _incomeRepo.Object,
        _limitRepo.Object,
        _uow.Object);

    private static readonly Guid UserId = Guid.NewGuid();

    private void SetupUser(bool exists = true)
    {
        var user = exists
            ? new User { Id = UserId, Email = "test@tests.budget.dev", PasswordHash = "hash", InitialBudget = 0 }
            : null;
        _userRepo.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    private void SetupEmptyRepos()
    {
        _expenseRepo.Setup(r => r.Query()).Returns(new List<DailyExpense>().AsAsyncQueryable());
        _incomeRepo.Setup(r => r.Query()).Returns(new List<DailyIncome>().AsAsyncQueryable());
        _limitRepo.Setup(r => r.Query()).Returns(new List<DailyLimit>().AsAsyncQueryable());
        _categoryRepo.Setup(r => r.Query()).Returns(new List<Category>().AsAsyncQueryable());
    }

    [Fact]
    public async Task DeleteAccountAsync_UserNotFound_ThrowsNotFoundException()
    {
        SetupUser(exists: false);
        SetupEmptyRepos();
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(() => sut.DeleteAccountAsync(UserId));
    }

    [Fact]
    public async Task DeleteAccountAsync_FreshAccount_RemovesUserAndSaves()
    {
        SetupUser();
        SetupEmptyRepos();
        var sut = CreateSut();

        await sut.DeleteAccountAsync(UserId);

        _userRepo.Verify(r => r.Remove(It.Is<User>(u => u.Id == UserId)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAccountAsync_WithData_RemovesAllEntitiesAndSavesOnce()
    {
        var categoryId = Guid.NewGuid();

        SetupUser();

        var expenses = new List<DailyExpense>
        {
            new() { Id = Guid.NewGuid(), UserId = UserId, CategoryId = categoryId, Date = new DateOnly(2026, 5, 1), Amount = 42m }
        };
        var incomes = new List<DailyIncome>
        {
            new() { Id = Guid.NewGuid(), UserId = UserId, Date = new DateOnly(2026, 5, 1), Amount = 200m }
        };
        var limits = new List<DailyLimit>
        {
            new() { Id = Guid.NewGuid(), UserId = UserId, EffectiveFromDate = new DateOnly(2026, 1, 1), Amount = 75m }
        };
        var categories = new List<Category>
        {
            new() { Id = categoryId, UserId = UserId, Name = "Groceries", IsArchived = false }
        };

        _expenseRepo.Setup(r => r.Query()).Returns(expenses.AsAsyncQueryable());
        _incomeRepo.Setup(r => r.Query()).Returns(incomes.AsAsyncQueryable());
        _limitRepo.Setup(r => r.Query()).Returns(limits.AsAsyncQueryable());
        _categoryRepo.Setup(r => r.Query()).Returns(categories.AsAsyncQueryable());

        var sut = CreateSut();

        await sut.DeleteAccountAsync(UserId);

        _expenseRepo.Verify(r => r.RemoveRange(It.Is<IEnumerable<DailyExpense>>(e => e.Count() == 1)), Times.Once);
        _incomeRepo.Verify(r => r.RemoveRange(It.Is<IEnumerable<DailyIncome>>(i => i.Count() == 1)), Times.Once);
        _limitRepo.Verify(r => r.RemoveRange(It.Is<IEnumerable<DailyLimit>>(l => l.Count() == 1)), Times.Once);
        _categoryRepo.Verify(r => r.RemoveRange(It.Is<IEnumerable<Category>>(c => c.Count() == 1)), Times.Once);
        _userRepo.Verify(r => r.Remove(It.Is<User>(u => u.Id == UserId)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAccountAsync_SavesOnlyOnce_RegardlessOfDataVolume()
    {
        SetupUser();
        SetupEmptyRepos();
        var sut = CreateSut();

        await sut.DeleteAccountAsync(UserId);

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
