using BudgetAnalyzer.Application.Abstractions;

namespace BudgetAnalyzer.Api.BackgroundServices;

public class TokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TokenCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public TokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<TokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var revocation = scope.ServiceProvider.GetRequiredService<ITokenRevocationService>();
                await revocation.DeleteExpiredAsync(stoppingToken);
                _logger.LogInformation("Expired revoked tokens purged.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purge expired revoked tokens.");
            }
        }
    }
}
