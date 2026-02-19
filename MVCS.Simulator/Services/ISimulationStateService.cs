using MVCS.Shared.DTOs;

namespace MVCS.Simulator.Services;

/// <summary>
/// Abstraction for simulation state management.
/// Thread-safe for concurrent access from background workers, hubs, and controllers.
/// </summary>
public interface ISimulationStateService
{
    SimulationStateDto State { get; }

    int CompassHeading { get; set; }
    double WaterLevel { get; set; }
    bool WaterRising { get; set; }
    bool PumpIsOn { get; set; }
    string LedHexColor { get; set; }
    int LedBrightness { get; set; }
    int CompassIntervalMs { get; set; }
    int WaterIntervalMs { get; set; }

    void Toggle(string component);
    void SetInterval(string component, int intervalMs);
    string GetCardinalDirection(int heading);
    string GetWaterStatus();
    bool GetComponentEnabled(string component);
    SimulationStateDto GetStateSnapshot();
}
