using MVCS.Shared.DTOs;

namespace MVCS.Simulator.Services;

public class SimulationStateService
{
    private readonly object _lock = new();

    public SimulationStateDto State { get; } = new()
    {
        IsGlobalRunning = true,
        IsCompassEnabled = true,
        IsWaterEnabled = true,
        IsPumpEnabled = true,
        IsLedEnabled = true
    };

    // Current sensor/actuator values
    public int CompassHeading { get; set; } = 0;
    public double WaterLevel { get; set; } = 50.0;
    public bool WaterRising { get; set; } = true;
    public bool PumpIsOn { get; set; } = false;
    public string LedHexColor { get; set; } = "#000000";
    public int LedBrightness { get; set; } = 100;

    public void Toggle(string component)
    {
        lock (_lock)
        {
            switch (component.ToLower())
            {
                case "compass":
                    State.IsCompassEnabled = !State.IsCompassEnabled;
                    break;
                case "water":
                    State.IsWaterEnabled = !State.IsWaterEnabled;
                    break;
                case "pump":
                    State.IsPumpEnabled = !State.IsPumpEnabled;
                    if (!State.IsPumpEnabled) PumpIsOn = false;
                    break;
                case "led":
                    State.IsLedEnabled = !State.IsLedEnabled;
                    break;
            }
        }
    }

    public string GetCardinalDirection(int heading)
    {
        return heading switch
        {
            >= 337 or < 23 => "N",
            >= 23 and < 68 => "NE",
            >= 68 and < 113 => "E",
            >= 113 and < 158 => "SE",
            >= 158 and < 203 => "S",
            >= 203 and < 248 => "SW",
            >= 248 and < 293 => "W",
            _ => "NW"
        };
    }
}
