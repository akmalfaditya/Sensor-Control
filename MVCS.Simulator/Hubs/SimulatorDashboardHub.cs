using Microsoft.AspNetCore.SignalR;
using MVCS.Simulator.Hubs;
using MVCS.Simulator.Services;

namespace MVCS.Simulator.Hubs;

/// <summary>
/// Local SignalR hub for browser clients connecting to the Simulator dashboard UI.
/// Separate from SimulatorHub which handles Server-to-Simulator commands.
/// </summary>
public class SimulatorDashboardHub : Hub
{
    private readonly ISimulationStateService _state;
    private readonly ILogger<SimulatorDashboardHub> _logger;

    public SimulatorDashboardHub(ISimulationStateService state, ILogger<SimulatorDashboardHub> logger)
    {
        _state = state;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Dashboard browser connected: {ConnectionId}", Context.ConnectionId);

        // Send current state immediately to new client
        await Clients.Caller.SendAsync("ReceiveHardwareState", _state.GetStateSnapshot());
        await Clients.Caller.SendAsync("ReceiveCompass", _state.CompassHeading,
            _state.GetCardinalDirection(_state.CompassHeading));
        await Clients.Caller.SendAsync("ReceivePumpState", _state.PumpIsOn,
            _state.PumpIsOn ? "Pump is running" : "Pump is idle");
        await Clients.Caller.SendAsync("ReceiveLedState", _state.LedHexColor, _state.LedBrightness);
        await Clients.Caller.SendAsync("ReceiveWaterLevel",
            Math.Round(_state.WaterLevel, 1), _state.GetWaterStatus());

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Dashboard browser disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
