using MVCS.Shared.DTOs;

namespace MVCS.Server.Services;

/// <summary>
/// Tracks the Simulator's inbound SignalR connection to VesselHub (data stream).
/// Used by VesselHub to detect connect/disconnect and cache last known state.
/// </summary>
public class SimulatorConnectionService
{
    /// <summary>The SignalR connection ID of the simulator client, or null if disconnected.</summary>
    public string? SimulatorConnectionId { get; set; }

    /// <summary>Whether the simulator is currently pushing data to VesselHub.</summary>
    public bool IsSimulatorConnected => SimulatorConnectionId != null;

    /// <summary>Last known hardware state from the simulator.</summary>
    public SimulationStateDto? LastKnownState { get; set; }
}
