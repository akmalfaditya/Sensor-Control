using Microsoft.AspNetCore.SignalR.Client;
using MVCS.Shared.DTOs;

namespace MVCS.Simulator.Services;

/// <summary>
/// Manages the outbound SignalR connection from Simulator to Server's VesselHub.
/// Used by workers & SimulatorHub to push sensor data and state updates to the dashboard.
/// This is the "broadcaster" side — Simulator pushes data TO Server.
/// </summary>
public class SimulatorHubClient : IHostedService, ISimulatorHubClient
{
    private HubConnection? _hub;
    private readonly ISimulationStateService _state;
    private readonly ILogger<SimulatorHubClient> _logger;
    private readonly string _serverHubUrl;

    public SimulatorHubClient(ISimulationStateService state, ILogger<SimulatorHubClient> logger, IConfiguration configuration)
    {
        _state = state;
        _logger = logger;
        _serverHubUrl = configuration["SignalR:ServerHubUrl"]
            ?? throw new InvalidOperationException("SignalR:ServerHubUrl is not configured in appsettings.json");
    }

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl($"{_serverHubUrl}?role=simulator")
            .WithAutomaticReconnect(new[]
            {
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
                _logger.LogInformation("Connected to Server SignalR hub at {Url}", _serverHubUrl);
                await PushHardwareStateAsync();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to connect to Server hub: {Message}. Retrying in 3s...", ex.Message);
                try { await Task.Delay(3000, ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    // ---- Push methods using SendAsync (fire-and-forget, more efficient) ----

    public async Task PushCompassAsync(int heading, string cardinal)
    {
        if (!IsConnected)
        {
            _logger.LogDebug("Skipping compass push — not connected to Server");
            return;
        }

        try { await _hub!.SendAsync("SimPushCompass", heading, cardinal); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to push compass data to Server hub"); }
    }

    public async Task PushWaterLevelAsync(double level, string status)
    {
        if (!IsConnected)
        {
            _logger.LogDebug("Skipping water level push — not connected to Server");
            return;
        }

        try { await _hub!.SendAsync("SimPushWaterLevel", level, status); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to push water level data to Server hub"); }
    }

    public async Task PushHardwareStateAsync()
    {
        if (!IsConnected)
        {
            _logger.LogDebug("Skipping hardware state push — not connected to Server");
            return;
        }

        try { await _hub!.SendAsync("SimPushHardwareState", _state.GetStateSnapshot()); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to push hardware state to Server hub"); }
    }

    public async Task PushPumpStateAsync(bool isOn, string message)
    {
        if (!IsConnected)
        {
            _logger.LogDebug("Skipping pump state push — not connected to Server");
            return;
        }

        try { await _hub!.SendAsync("SimPushPumpState", isOn, message); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to push pump state to Server hub"); }
    }

    public async Task PushLedStateAsync(string hexColor, int brightness)
    {
        if (!IsConnected)
        {
            _logger.LogDebug("Skipping LED state push — not connected to Server");
            return;
        }

        try { await _hub!.SendAsync("SimPushLedState", hexColor, brightness); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to push LED state to Server hub"); }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_hub != null)
        {
            await _hub.DisposeAsync();
        }
    }
}
