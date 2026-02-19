using Microsoft.AspNetCore.SignalR;
using MVCS.Server.Services;
using MVCS.Shared.DTOs;

namespace MVCS.Server.Hubs;

/// <summary>
/// SignalR hub hosted on Server (port 5000).
/// Dashboard JS clients connect here for real-time updates.
/// Simulator connects here as a client to push sensor data.
/// This is the "listener" side — Server receives data FROM Simulator.
/// </summary>
public class VesselHub : Hub
{
    private readonly ISimulatorConnectionService _simConn;
    private readonly ILogService _logService;
    private readonly ILogger<VesselHub> _logger;

    public VesselHub(ISimulatorConnectionService simConn, ILogService logService, ILogger<VesselHub> logger)
    {
        _simConn = simConn;
        _logService = logService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var role = Context.GetHttpContext()?.Request.Query["role"].ToString();
        if (role == "simulator")
        {
            _simConn.SimulatorConnectionId = Context.ConnectionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, "Simulator");

            // Push cached state to dashboard on simulator connect
            if (_simConn.LastKnownState != null)
            {
                await Clients.Group("Dashboard").SendAsync("ReceiveHardwareState", _simConn.LastKnownState);
            }

            _logger.LogInformation("Simulator connected: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Dashboard");
            await Clients.Caller.SendAsync("ConnectionStatus", true);
            _logger.LogInformation("Dashboard client connected: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.ConnectionId == _simConn.SimulatorConnectionId)
        {
            _simConn.SimulatorConnectionId = null;
            // Notify dashboard that simulator's data stream went offline
            var offlineState = new SimulationStateDto { IsGlobalRunning = false };
            await Clients.Group("Dashboard").SendAsync("ReceiveHardwareState", offlineState);
            _logger.LogWarning("Simulator disconnected: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ---- Methods the Simulator calls to push data (with input validation) ----

    /// <summary>Simulator pushes compass data</summary>
    public async Task SimPushCompass(int heading, string cardinal)
    {
        if (heading < 0 || heading >= 360)
        {
            _logger.LogWarning("Invalid compass heading received: {Heading}", heading);
            return;
        }

        if (string.IsNullOrWhiteSpace(cardinal))
        {
            _logger.LogWarning("Empty cardinal direction received");
            return;
        }

        await _logService.LogCompassAsync(heading, cardinal);
        await Clients.Group("Dashboard").SendAsync("ReceiveCompass", heading, cardinal);
    }

    /// <summary>Simulator pushes water level data</summary>
    public async Task SimPushWaterLevel(double level, string status)
    {
        if (level < 0 || level > 100)
        {
            _logger.LogWarning("Invalid water level received: {Level}", level);
            return;
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            _logger.LogWarning("Empty water status received");
            return;
        }

        await _logService.LogWaterLevelAsync(level, status);
        await Clients.Group("Dashboard").SendAsync("ReceiveWaterLevel", level, status);
    }

    /// <summary>Simulator pushes hardware state (on toggle or periodically)</summary>
    public async Task SimPushHardwareState(SimulationStateDto state)
    {
        if (state == null)
        {
            _logger.LogWarning("Null hardware state received");
            return;
        }

        _simConn.LastKnownState = state;
        await Clients.Group("Dashboard").SendAsync("ReceiveHardwareState", state);
    }

    /// <summary>Simulator pushes pump state after command execution</summary>
    public async Task SimPushPumpState(bool isOn, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            message = isOn ? "Pump activated" : "Pump deactivated";

        await _logService.LogPumpAsync(isOn, message);
        await Clients.Group("Dashboard").SendAsync("ReceivePumpState", isOn, message);
    }

    /// <summary>Simulator pushes LED state after command execution</summary>
    public async Task SimPushLedState(string hexColor, int brightness)
    {
        if (string.IsNullOrWhiteSpace(hexColor))
            hexColor = "#000000";

        if (brightness < 0 || brightness > 100)
        {
            _logger.LogWarning("Invalid LED brightness received: {Brightness}", brightness);
            brightness = Math.Clamp(brightness, 0, 100);
        }

        await _logService.LogLedAsync(hexColor);
        await Clients.Group("Dashboard").SendAsync("ReceiveLedState", hexColor, brightness);
    }
}
