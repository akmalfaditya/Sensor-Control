using MVCS.Server.Models;

namespace MVCS.Server.Services;

/// <summary>
/// Abstraction for logging sensor/actuator data to the database.
/// </summary>
public interface ILogService
{
    Task LogCompassAsync(int heading, string cardinal);
    Task LogWaterLevelAsync(double level, string status);
    Task LogPumpAsync(bool isOn, string message);
    Task LogLedAsync(string hexColor);

    Task<List<WaterLevelLog>> GetWaterHistoryAsync(int page = 1, int pageSize = 50, DateTime? from = null, DateTime? to = null);
    Task<List<PumpLog>> GetPumpLogsAsync(int page = 1, int pageSize = 50);
    Task<List<CompassLog>> GetCompassLogsAsync(int page = 1, int pageSize = 50);
    Task<List<LedLog>> GetLedLogsAsync(int page = 1, int pageSize = 50);
}
