using Microsoft.AspNetCore.SignalR.Client;
using MVCS.Shared.DTOs;

namespace MVCS.Simulator.Services;

/// <summary>
/// Manages the outbound SignalR connection from Simulator to Server's VesselHub (port 5000).
/// Used by workers & SimulatorHub to push sensor data and state updates to the dashboard.
/// This is the "broadcaster" side — Simulator pushes data TO Server.
/// </summary>
public class SimulatorHubClient : IHostedService
{
    private HubConnection? _hub;
    private readonly SimulationStateService _state;
    private readonly ILogger<SimulatorHubClient> _logger;

    public SimulatorHubClient(SimulationStateService state, ILogger<SimulatorHubClient> logger)
    {
        _state = state;
        _logger = logger;
    }

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/vesselhub?role=simulator")
            .WithAutomaticReconnect(new[] {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _hub.Reconnecting += ex =>
        {
            _logger.LogWarning("Reconnecting to Server hub: {Message}", ex?.Message);
            return Task.CompletedTask;
        };

        _hub.Reconnected += connectionId =>
        {
            _logger.LogInformation("Reconnected to Server hub: {ConnectionId}", connectionId);
            _ = PushHardwareStateAsync();
            return Task.CompletedTask;
        };

        _hub.Closed += ex =>
        {
            _logger.LogWarning("Connection to Server hub closed: {Message}", ex?.Message);
            return Task.CompletedTask;
        };

        // Connect with retry (non-blocking — Simulator keeps running if Server is down)
        _ = ConnectWithRetryAsync(cancellationToken);

        return Task.CompletedTask;
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _hub!.StartAsync(ct);
                _logger.LogInformation("Connected to Server SignalR hub at :5000");
                await PushHardwareStateAsync();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to connect to Server hub: {Message}. Retrying in 3s...", ex.Message);
                try { await Task.Delay(3000, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    // ---- Push methods for workers / SimulatorHub ----

    public async Task PushCompassAsync(int heading, string cardinal)
    {
        if (!IsConnected) return;
        try { await _hub!.InvokeAsync("SimPushCompass", heading, cardinal); }
        catch (Exception ex) { _logger.LogWarning("Failed to push compass: {Message}", ex.Message); }
    }

    public async Task PushWaterLevelAsync(double level, string status)
    {
        if (!IsConnected) return;
        try { await _hub!.InvokeAsync("SimPushWaterLevel", level, status); }
        catch (Exception ex) { _logger.LogWarning("Failed to push water level: {Message}", ex.Message); }
    }

    public async Task PushHardwareStateAsync()
    {
        if (!IsConnected) return;
        try { await _hub!.InvokeAsync("SimPushHardwareState", _state.State); }
        catch (Exception ex) { _logger.LogWarning("Failed to push hardware state: {Message}", ex.Message); }
    }

    public async Task PushPumpStateAsync(bool isOn, string message)
    {
        if (!IsConnected) return;
        try { await _hub!.InvokeAsync("SimPushPumpState", isOn, message); }
        catch (Exception ex) { _logger.LogWarning("Failed to push pump state: {Message}", ex.Message); }
    }

    public async Task PushLedStateAsync(string hexColor, int brightness)
    {
        if (!IsConnected) return;
        try { await _hub!.InvokeAsync("SimPushLedState", hexColor, brightness); }
        catch (Exception ex) { _logger.LogWarning("Failed to push LED state: {Message}", ex.Message); }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_hub != null)
        {
            await _hub.DisposeAsync();
        }
    }
}
