using BudgetAnalyzer.Application.Import;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BudgetAnalyzer.Infrastructure.BackgroundServices;

public class TempFileCleanupService : BackgroundService
{
    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaxFileAge  = TimeSpan.FromHours(1);

    private readonly ITempFileStore _store;
    private readonly ILogger<TempFileCleanupService> _logger;

    public TempFileCleanupService(ITempFileStore store, ILogger<TempFileCleanupService> logger)
    {
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(RunInterval, stoppingToken);
            var threshold = DateTime.UtcNow - MaxFileAge;

            foreach (var (fileId, createdAt) in _store.ListAll().ToList())
            {
                if (createdAt < threshold)
                {
                    _store.Delete(fileId);
                    _logger.LogInformation(
                        "Deleted stale import temp file {FileId} (created {CreatedAt:O})",
                        fileId, createdAt);
                }
            }
        }
    }
}
