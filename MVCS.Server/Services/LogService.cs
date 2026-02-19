using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using MVCS.Server.Data;
using MVCS.Server.Models;

namespace MVCS.Server.Services;

/// <summary>
/// Buffered log service that batches sensor data writes for better performance.
/// Uses Channel&lt;T&gt; to queue log entries and flushes them periodically.
/// Supports pagination and date-range filtering for history queries.
/// </summary>
public class LogService : ILogService, IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogService> _logger;
    private readonly Channel<LogEntry> _channel;
    private readonly Timer _flushTimer;
    private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(5);

    public LogService(IServiceScopeFactory scopeFactory, ILogger<LogService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _flushTimer = new Timer(FlushCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _flushTimer.Change(_flushInterval, _flushInterval);
        _logger.LogInformation("BufferedLogService started — flushing every {Interval}s", _flushInterval.TotalSeconds);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);
        // Final flush
        FlushCallback(null);
        _logger.LogInformation("BufferedLogService stopped");
        return Task.CompletedTask;
    }

    // ---- Write methods (buffered) ----

    public async Task LogCompassAsync(int heading, string cardinal)
    {
        await _channel.Writer.WriteAsync(new LogEntry
        {
            Type = LogType.Compass,
            IntValue = heading,
            StringValue = cardinal,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task LogWaterLevelAsync(double level, string status)
    {
        await _channel.Writer.WriteAsync(new LogEntry
        {
            Type = LogType.WaterLevel,
            DoubleValue = level,
            StringValue = status,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task LogPumpAsync(bool isOn, string message)
    {
        await _channel.Writer.WriteAsync(new LogEntry
        {
            Type = LogType.Pump,
            BoolValue = isOn,
            StringValue = message,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task LogLedAsync(string hexColor)
    {
        await _channel.Writer.WriteAsync(new LogEntry
        {
            Type = LogType.Led,
            StringValue = hexColor,
            Timestamp = DateTime.UtcNow
        });
    }

    // ---- Read methods (paginated) ----

    public async Task<List<WaterLevelLog>> GetWaterHistoryAsync(int page = 1, int pageSize = 50, DateTime? from = null, DateTime? to = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var query = db.WaterLevelLogs.AsQueryable();
        if (from.HasValue) query = query.Where(x => x.Timestamp >= from.Value);
        if (to.HasValue) query = query.Where(x => x.Timestamp <= to.Value);

        return await query
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .OrderBy(x => x.Timestamp)
            .ToListAsync();
    }

    public async Task<List<PumpLog>> GetPumpLogsAsync(int page = 1, int pageSize = 50)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.PumpLogs
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<CompassLog>> GetCompassLogsAsync(int page = 1, int pageSize = 50)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.CompassLogs
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<LedLog>> GetLedLogsAsync(int page = 1, int pageSize = 50)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await db.LedLogs
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    // ---- Flush logic ----

    private async void FlushCallback(object? state)
    {
        try
        {
            var entries = new List<LogEntry>();
            while (_channel.Reader.TryRead(out var entry))
                entries.Add(entry);

            if (entries.Count == 0) return;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            foreach (var entry in entries)
            {
                switch (entry.Type)
                {
                    case LogType.Compass:
                        db.CompassLogs.Add(new CompassLog
                        {
                            Heading = entry.IntValue,
                            Cardinal = entry.StringValue ?? string.Empty,
                            Timestamp = entry.Timestamp
                        });
                        break;

                    case LogType.WaterLevel:
                        db.WaterLevelLogs.Add(new WaterLevelLog
                        {
                            Level = entry.DoubleValue,
                            Status = entry.StringValue ?? string.Empty,
                            Timestamp = entry.Timestamp
                        });
                        break;

                    case LogType.Pump:
                        db.PumpLogs.Add(new PumpLog
                        {
                            IsOn = entry.BoolValue,
                            Message = entry.StringValue ?? string.Empty,
                            Timestamp = entry.Timestamp
                        });
                        break;

                    case LogType.Led:
                        db.LedLogs.Add(new LedLog
                        {
                            HexColor = entry.StringValue ?? "#000000",
                            Timestamp = entry.Timestamp
                        });
                        break;
                }
            }

            await db.SaveChangesAsync();
            _logger.LogDebug("Flushed {Count} log entries to database", entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing log entries to database");
        }
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
    }

    // ---- Internal types ----

    private enum LogType { Compass, WaterLevel, Pump, Led }

    private class LogEntry
    {
        public LogType Type { get; init; }
        public int IntValue { get; init; }
        public double DoubleValue { get; init; }
        public bool BoolValue { get; init; }
        public string? StringValue { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
