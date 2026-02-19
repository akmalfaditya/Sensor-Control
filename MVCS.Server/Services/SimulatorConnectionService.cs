using MVCS.Shared.DTOs;

namespace MVCS.Server.Services;

/// <summary>
/// Tracks the Simulator's inbound SignalR connection to VesselHub (data stream).
/// Thread-safe — accessed from SignalR hub methods running on thread pool threads.
/// </summary>
public class SimulatorConnectionService : ISimulatorConnectionService
{
    private readonly object _lock = new();
    private string? _simulatorConnectionId;
    private SimulationStateDto? _lastKnownState;

    /// <summary>The SignalR connection ID of the simulator client, or null if disconnected.</summary>
    public string? SimulatorConnectionId
    {
        get { lock (_lock) return _simulatorConnectionId; }
        set { lock (_lock) _simulatorConnectionId = value; }
    }

    /// <summary>Whether the simulator is currently pushing data to VesselHub.</summary>
    public bool IsSimulatorConnected
    {
        get { lock (_lock) return _simulatorConnectionId != null; }
    }

    /// <summary>Last known hardware state from the simulator.</summary>
    public SimulationStateDto? LastKnownState
    {
        get { lock (_lock) return _lastKnownState; }
        set { lock (_lock) _lastKnownState = value; }
    }
}
