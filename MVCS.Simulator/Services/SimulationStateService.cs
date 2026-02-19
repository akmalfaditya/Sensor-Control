using MVCS.Shared.DTOs;

namespace MVCS.Simulator.Services;

/// <summary>
/// Manages all simulation state with full thread safety.
/// Singleton service accessed by background workers, SignalR hubs, and API controllers concurrently.
/// </summary>
public class SimulationStateService : ISimulationStateService
{
    private readonly object _lock = new();

    // Internal mutable state DTO — only mutated under lock
    private readonly SimulationStateDto _state = new()
    {
        IsGlobalRunning = true,
        IsCompassEnabled = true,
        IsWaterEnabled = true,
        IsPumpEnabled = true,
        IsLedEnabled = true
    };

    // Backing fields for sensor/actuator values
    private int _compassHeading;
    private double _waterLevel = 50.0;
    private bool _waterRising = true;
    private bool _pumpIsOn;
    private string _ledHexColor = "#000000";
    private int _ledBrightness = 100;

    /// <summary>Returns the internal state reference. For thread-safe snapshots, use GetStateSnapshot().</summary>
    public SimulationStateDto State
    {
        get { lock (_lock) return _state; }
    }

    public int CompassHeading
    {
        get { lock (_lock) return _compassHeading; }
        set { lock (_lock) _compassHeading = value; }
    }

    public double WaterLevel
    {
        get { lock (_lock) return _waterLevel; }
        set { lock (_lock) _waterLevel = value; }
    }

    public bool WaterRising
    {
        get { lock (_lock) return _waterRising; }
        set { lock (_lock) _waterRising = value; }
    }

    public bool PumpIsOn
    {
        get { lock (_lock) return _pumpIsOn; }
        set { lock (_lock) _pumpIsOn = value; }
    }

    public string LedHexColor
    {
        get { lock (_lock) return _ledHexColor; }
        set { lock (_lock) _ledHexColor = value; }
    }

    public int LedBrightness
    {
        get { lock (_lock) return _ledBrightness; }
        set { lock (_lock) _ledBrightness = value; }
    }

    public int CompassIntervalMs
    {
        get { lock (_lock) return _state.CompassIntervalMs; }
        set { lock (_lock) _state.CompassIntervalMs = Math.Clamp(value, 100, 10000); }
    }

    public int WaterIntervalMs
    {
        get { lock (_lock) return _state.WaterIntervalMs; }
        set { lock (_lock) _state.WaterIntervalMs = Math.Clamp(value, 100, 10000); }
    }

    public void SetInterval(string component, int intervalMs)
    {
        lock (_lock)
        {
            switch (component.ToLower())
            {
                case "compass":
                    _state.CompassIntervalMs = Math.Clamp(intervalMs, 100, 10000);
                    break;
                case "water":
                    _state.WaterIntervalMs = Math.Clamp(intervalMs, 100, 10000);
                    break;
            }
        }
    }

    public void Toggle(string component)
    {
        lock (_lock)
        {
            switch (component.ToLower())
            {
                case "compass":
                    _state.IsCompassEnabled = !_state.IsCompassEnabled;
                    break;
                case "water":
                    _state.IsWaterEnabled = !_state.IsWaterEnabled;
                    break;
                case "pump":
                    _state.IsPumpEnabled = !_state.IsPumpEnabled;
                    if (!_state.IsPumpEnabled) _pumpIsOn = false;
                    break;
                case "led":
                    _state.IsLedEnabled = !_state.IsLedEnabled;
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

    /// <summary>Returns the water status based on current level — centralized to avoid duplication.</summary>
    public string GetWaterStatus()
    {
        lock (_lock)
        {
            return _waterLevel switch
            {
                >= 80 => "HIGH",
                >= 20 => "NORMAL",
                _ => "LOW"
            };
        }
    }

    /// <summary>Returns whether a specific hardware component is enabled.</summary>
    public bool GetComponentEnabled(string component)
    {
        lock (_lock)
        {
            return component.ToLower() switch
            {
                "compass" => _state.IsCompassEnabled,
                "water" => _state.IsWaterEnabled,
                "pump" => _state.IsPumpEnabled,
                "led" => _state.IsLedEnabled,
                _ => false
            };
        }
    }

    /// <summary>Returns a thread-safe snapshot of the current state.</summary>
    public SimulationStateDto GetStateSnapshot()
    {
        lock (_lock)
        {
            return new SimulationStateDto
            {
                IsGlobalRunning = _state.IsGlobalRunning,
                IsCompassEnabled = _state.IsCompassEnabled,
                IsWaterEnabled = _state.IsWaterEnabled,
                IsPumpEnabled = _state.IsPumpEnabled,
                IsLedEnabled = _state.IsLedEnabled,
                CompassIntervalMs = _state.CompassIntervalMs,
                WaterIntervalMs = _state.WaterIntervalMs
            };
        }
    }
}
