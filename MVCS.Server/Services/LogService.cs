using MVCS.Server.Data;
using MVCS.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MVCS.Server.Services;

public class LogService
{
    private readonly ApplicationDbContext _db;

    public LogService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogCompassAsync(int heading, string cardinal)
    {
        _db.CompassLogs.Add(new CompassLog
        {
            Heading = heading,
            Cardinal = cardinal,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task LogWaterLevelAsync(double level, string status)
    {
        _db.WaterLevelLogs.Add(new WaterLevelLog
        {
            Level = level,
            Status = status,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task LogPumpAsync(bool isOn, string message)
    {
        _db.PumpLogs.Add(new PumpLog
        {
            IsOn = isOn,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task LogLedAsync(string hexColor)
    {
        _db.LedLogs.Add(new LedLog
        {
            HexColor = hexColor,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task<List<WaterLevelLog>> GetWaterHistoryAsync(int count = 50)
    {
        return await _db.WaterLevelLogs
            .OrderByDescending(x => x.Timestamp)
            .Take(count)
            .OrderBy(x => x.Timestamp)
            .ToListAsync();
    }

    public async Task<List<PumpLog>> GetPumpLogsAsync(int count = 50)
    {
        return await _db.PumpLogs
            .OrderByDescending(x => x.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<CompassLog>> GetCompassLogsAsync(int count = 50)
    {
        return await _db.CompassLogs
            .OrderByDescending(x => x.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<LedLog>> GetLedLogsAsync(int count = 50)
    {
        return await _db.LedLogs
            .OrderByDescending(x => x.Timestamp)
            .Take(count)
            .ToListAsync();
    }
}
