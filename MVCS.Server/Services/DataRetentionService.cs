using Microsoft.EntityFrameworkCore;
using MVCS.Server.Data;

namespace MVCS.Server.Services;

/// <summary>
/// Background service that cleans up old sensor log data.
/// Runs once every 24 hours and deletes records older than 30 days.
/// </summary>
public class DataRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRetentionService> _logger;
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(30);
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

    public DataRetentionService(IServiceScopeFactory scopeFactory, ILogger<DataRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataRetentionService started — cleaning records older than {Days} days", _retentionPeriod.TotalDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldRecordsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data retention cleanup");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CleanupOldRecordsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cutoff = DateTime.UtcNow - _retentionPeriod;

        var compassDeleted = await db.CompassLogs.Where(x => x.Timestamp < cutoff).ExecuteDeleteAsync(ct);
        var waterDeleted = await db.WaterLevelLogs.Where(x => x.Timestamp < cutoff).ExecuteDeleteAsync(ct);
        var pumpDeleted = await db.PumpLogs.Where(x => x.Timestamp < cutoff).ExecuteDeleteAsync(ct);
        var ledDeleted = await db.LedLogs.Where(x => x.Timestamp < cutoff).ExecuteDeleteAsync(ct);

        var total = compassDeleted + waterDeleted + pumpDeleted + ledDeleted;
        if (total > 0)
        {
            _logger.LogInformation(
                "Data retention cleanup: deleted {Total} records (compass={Compass}, water={Water}, pump={Pump}, led={Led})",
                total, compassDeleted, waterDeleted, pumpDeleted, ledDeleted);
        }
        else
        {
            _logger.LogDebug("Data retention cleanup: no records older than {Cutoff}", cutoff);
        }
    }
}
