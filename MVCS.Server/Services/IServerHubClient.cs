using MVCS.Shared.DTOs;

namespace MVCS.Server.Services;

/// <summary>
/// Abstraction for the outbound SignalR client from Server to Simulator.
/// Used to send commands (pump, LED, toggle) to the Simulator.
/// </summary>
public interface IServerHubClient
{
    bool IsConnected { get; }
    Task<string> SendPumpCommandAsync(bool isOn, string message);
    Task<string> SendLedCommandAsync(string hexColor, int brightness);
    Task<SimulationStateDto?> SendToggleAsync(string component);
    Task<SimulationStateDto?> RequestStateAsync();
}
