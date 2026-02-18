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
    private readonly SimulatorConnectionService _simConn;
    private readonly LogService _logService;

    public VesselHub(SimulatorConnectionService simConn, LogService logService)
    {
        _simConn = simConn;
        _logService = logService;
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
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Dashboard");
            await Clients.Caller.SendAsync("ConnectionStatus", true);
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
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ---- Methods the Simulator calls to push data ----

    /// <summary>Simulator pushes compass data</summary>
    public async Task SimPushCompass(int heading, string cardinal)
    {
        await _logService.LogCompassAsync(heading, cardinal);
        await Clients.Group("Dashboard").SendAsync("ReceiveCompass", heading, cardinal);
    }

    /// <summary>Simulator pushes water level data</summary>
    public async Task SimPushWaterLevel(double level, string status)
    {
        await _logService.LogWaterLevelAsync(level, status);
        await Clients.Group("Dashboard").SendAsync("ReceiveWaterLevel", level, status);
    }

    /// <summary>Simulator pushes hardware state (on toggle or periodically)</summary>
    public async Task SimPushHardwareState(SimulationStateDto state)
    {
        _simConn.LastKnownState = state;
        await Clients.Group("Dashboard").SendAsync("ReceiveHardwareState", state);
    }

    /// <summary>Simulator pushes pump state after command execution</summary>
    public async Task SimPushPumpState(bool isOn, string message)
    {
        await _logService.LogPumpAsync(isOn, message);
        await Clients.Group("Dashboard").SendAsync("ReceivePumpState", isOn, message);
    }

    /// <summary>Simulator pushes LED state after command execution</summary>
    public async Task SimPushLedState(string hexColor, int brightness)
    {
        await _logService.LogLedAsync(hexColor);
        await Clients.Group("Dashboard").SendAsync("ReceiveLedState", hexColor, brightness);
    }
}
