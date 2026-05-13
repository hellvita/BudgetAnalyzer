using BudgetAnalyzer.Application.Abstractions;
using BudgetAnalyzer.Domain.Entities;
using BudgetAnalyzer.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BudgetAnalyzer.Application.Auth;

public class AuthService
{
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IClock _clock;

    public AuthService(
        IRepository<User> users,
        IUnitOfWork uow,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IClock clock)
    {
        _users = users;
        _uow = uow;
        _hasher = hasher;
        _jwt = jwt;
        _clock = clock;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var emailLower = request.Email.Trim().ToLowerInvariant();

        var exists = await _users.Query()
            .AnyAsync(u => u.Email == emailLower, ct);

        if (exists)
            throw new ConflictException($"Email '{request.Email}' is already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = emailLower,
            PasswordHash = _hasher.Hash(request.Password),
            InitialBudget = 0,
            CreatedAt = _clock.UtcNow,
            UpdatedAt = _clock.UtcNow,
        };

        _users.Add(user);
        await _uow.SaveChangesAsync(ct);

        var result = _jwt.Issue(user);
        return new AuthResponse(result.Token, result.ExpiresAt);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var emailLower = request.Email.Trim().ToLowerInvariant();

        var user = await _users.Query()
            .FirstOrDefaultAsync(u => u.Email == emailLower, ct)
            ?? throw new NotFoundException("Invalid email or password.");

        if (!_hasher.Verify(request.Password, user.PasswordHash))
            throw new NotFoundException("Invalid email or password.");

        var result = _jwt.Issue(user);
        return new AuthResponse(result.Token, result.ExpiresAt);
    }
}
