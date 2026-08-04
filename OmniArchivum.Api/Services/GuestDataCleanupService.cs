namespace OmniArchivum.Api.Services;

/// <summary>
/// Schedules <see cref="GuestDataCleaner"/>. Every visitor gets their own copy of the
/// demo archive, so without this the database would grow by roughly seven notes per
/// visit indefinitely.
/// </summary>
public sealed class GuestDataCleanupService : BackgroundService
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(2);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceProvider _services;
    private readonly ILogger<GuestDataCleanupService> _logger;

    public GuestDataCleanupService(IServiceProvider services, ILogger<GuestDataCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var cleaner = scope.ServiceProvider.GetRequiredService<GuestDataCleaner>();

                var reclaimed = await cleaner.PurgeStaleGuestsAsync(Retention, stoppingToken);

                if (reclaimed > 0)
                {
                    _logger.LogInformation("Reclaimed {SessionCount} stale guest sessions", reclaimed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A cleanup failure must never take the app down; it retries next cycle.
                _logger.LogError(ex, "Guest data cleanup failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
