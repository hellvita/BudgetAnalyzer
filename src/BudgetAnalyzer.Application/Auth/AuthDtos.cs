using System.ComponentModel.DataAnnotations;

namespace BudgetAnalyzer.Application.Auth;

public record RegisterRequest(
    [Required][EmailAddress] string Email,
    [Required][MinLength(8)] string Password);

public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(string Token, DateTime ExpiresAt);
