namespace MVCS.Simulator.Services;

/// <summary>
/// Abstraction for the outbound SignalR client from Simulator to Server's VesselHub.
/// Used by workers and SimulatorHub to push sensor data and state updates.
/// </summary>
public interface ISimulatorHubClient
{
    bool IsConnected { get; }
    Task PushCompassAsync(int heading, string cardinal);
    Task PushWaterLevelAsync(double level, string status);
    Task PushHardwareStateAsync();
    Task PushPumpStateAsync(bool isOn, string message);
    Task PushLedStateAsync(string hexColor, int brightness);
}
