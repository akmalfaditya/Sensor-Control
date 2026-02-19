namespace MVCS.Shared.DTOs;

public class SimulationStateDto
{
    public bool IsGlobalRunning { get; set; }
    public bool IsCompassEnabled { get; set; } = true;
    public bool IsWaterEnabled { get; set; } = true;
    public bool IsPumpEnabled { get; set; } = true;
    public bool IsLedEnabled { get; set; } = true;
    public int CompassIntervalMs { get; set; } = 500;
    public int WaterIntervalMs { get; set; } = 2000;
}
