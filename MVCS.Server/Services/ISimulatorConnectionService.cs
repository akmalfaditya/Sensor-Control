using MVCS.Shared.DTOs;

namespace MVCS.Server.Services;

/// <summary>
/// Tracks the Simulator's inbound SignalR connection state.
/// Thread-safe for concurrent access from SignalR hub methods.
/// </summary>
public interface ISimulatorConnectionService
{
    string? SimulatorConnectionId { get; set; }
    bool IsSimulatorConnected { get; }
    SimulationStateDto? LastKnownState { get; set; }
}
