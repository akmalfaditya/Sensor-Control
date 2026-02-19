using Microsoft.AspNetCore.SignalR;
using MVCS.Shared.DTOs;
using MVCS.Simulator.Services;

namespace MVCS.Simulator.Hubs;

/// <summary>
/// SignalR hub hosted on the Simulator.
/// The Server connects here as a client to send commands.
/// Hub methods return values directly — no correlation IDs needed.
/// Also broadcasts state changes to the local dashboard via SimulatorDashboardHub.
/// </summary>
public class SimulatorHub : Hub
{
    private readonly ISimulationStateService _state;
    private readonly ISimulatorHubClient _hubClient;
    private readonly IHubContext<SimulatorDashboardHub> _dashboardHub;
    private readonly ILogger<SimulatorHub> _logger;

    public SimulatorHub(ISimulationStateService state, ISimulatorHubClient hubClient,
        IHubContext<SimulatorDashboardHub> dashboardHub, ILogger<SimulatorHub> logger)
    {
        _state = state;
        _hubClient = hubClient;
        _dashboardHub = dashboardHub;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Server connected to SimulatorHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogWarning("Server disconnected from SimulatorHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ---- Commands from Server ----

    /// <summary>Server commands the pump on/off. Returns PumpStateDto or error.</summary>
    public async Task<object> ExecutePumpCommand(bool isOn, string message)
    {
        if (!_state.State.IsPumpEnabled)
            return new { error = "Pump is disabled", disabled = true };

        _state.PumpIsOn = isOn;
        var result = new PumpStateDto
        {
            IsOn = _state.PumpIsOn,
            Message = _state.PumpIsOn ? "Pump activated" : "Pump deactivated"
        };

        // Broadcast pump state to Server's dashboard via our outbound connection
        await _hubClient.PushPumpStateAsync(result.IsOn, result.Message);

        // Broadcast to local dashboard
        await _dashboardHub.Clients.All.SendAsync("ReceivePumpState", result.IsOn, result.Message);

        _logger.LogInformation("Pump command executed: IsOn={IsOn}", result.IsOn);
        return result;
    }

    /// <summary>Server commands the LED color/brightness. Returns LedStateDto or error.</summary>
    public async Task<object> ExecuteLedCommand(string hexColor, int brightness)
    {
        if (!_state.State.IsLedEnabled)
            return new { error = "LED is disabled", disabled = true };

        _state.LedHexColor = hexColor;
        _state.LedBrightness = brightness;
        var result = new LedStateDto
        {
            HexColor = _state.LedHexColor,
            Brightness = _state.LedBrightness
        };

        // Broadcast LED state to Server's dashboard via our outbound connection
        await _hubClient.PushLedStateAsync(result.HexColor, result.Brightness);

        // Broadcast to local dashboard
        await _dashboardHub.Clients.All.SendAsync("ReceiveLedState", result.HexColor, result.Brightness);

        _logger.LogInformation("LED command executed: Color={Color}, Brightness={Brightness}", result.HexColor, result.Brightness);
        return result;
    }

    /// <summary>Server asks to toggle a hardware component.</summary>
    public async Task<SimulationStateDto> ToggleHardware(string component)
    {
        _state.Toggle(component);
        _logger.LogInformation("Hardware toggled: {Component}", component);

        // Push updated state to Server's dashboard
        await _hubClient.PushHardwareStateAsync();

        // Push to local dashboard
        var snapshot = _state.GetStateSnapshot();
        await _dashboardHub.Clients.All.SendAsync("ReceiveHardwareState", snapshot);

        return snapshot;
    }

    /// <summary>Server requests current simulation state.</summary>
    public SimulationStateDto RequestState()
    {
        return _state.GetStateSnapshot();
    }
}
