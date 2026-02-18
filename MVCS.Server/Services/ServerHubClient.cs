using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using MVCS.Shared.DTOs;

namespace MVCS.Server.Services;

/// <summary>
/// Manages the SignalR client connection from Server to Simulator's hub (port 5100).
/// Used by VesselApiController to send commands (pump, LED, toggle).
/// Auto-reconnects independently — Server doesn't depend on Simulator being up.
/// </summary>
public class ServerHubClient : IHostedService
{
    private HubConnection? _hub;
    private readonly ILogger<ServerHubClient> _logger;

    public ServerHubClient(ILogger<ServerHubClient> logger)
    {
        _logger = logger;
    }

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl("http://localhost:5100/simulatorhub")
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
            _logger.LogWarning("Reconnecting to Simulator hub: {Message}", ex?.Message);
            return Task.CompletedTask;
        };

        _hub.Reconnected += connectionId =>
        {
            _logger.LogInformation("Reconnected to Simulator hub: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _hub.Closed += ex =>
        {
            _logger.LogWarning("Connection to Simulator hub closed: {Message}", ex?.Message);
            return Task.CompletedTask;
        };

        // Connect with retry (non-blocking — Server keeps running if Simulator is down)
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
                _logger.LogInformation("Connected to Simulator SignalR hub at :5100");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to connect to Simulator hub: {Message}. Retrying in 3s...", ex.Message);
                try { await Task.Delay(3000, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    // ---- Commands to Simulator (return values via InvokeAsync) ----

    /// <summary>Send pump command. Returns PumpStateDto or error object as JSON.</summary>
    public async Task<string> SendPumpCommandAsync(bool isOn, string message)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Simulator");

        var result = await _hub!.InvokeAsync<object>("ExecutePumpCommand", isOn, message);
        return JsonSerializer.Serialize(result);
    }

    /// <summary>Send LED command. Returns LedStateDto or error object as JSON.</summary>
    public async Task<string> SendLedCommandAsync(string hexColor, int brightness)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Simulator");

        var result = await _hub!.InvokeAsync<object>("ExecuteLedCommand", hexColor, brightness);
        return JsonSerializer.Serialize(result);
    }

    /// <summary>Toggle a hardware component. Returns updated SimulationStateDto.</summary>
    public async Task<SimulationStateDto?> SendToggleAsync(string component)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Simulator");

        return await _hub!.InvokeAsync<SimulationStateDto>("ToggleHardware", component);
    }

    /// <summary>Request current simulation state.</summary>
    public async Task<SimulationStateDto?> RequestStateAsync()
    {
        if (!IsConnected)
            return null;

        try
        {
            return await _hub!.InvokeAsync<SimulationStateDto>("RequestState");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to request state from Simulator: {Message}", ex.Message);
            return null;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_hub != null)
        {
            await _hub.DisposeAsync();
        }
    }
}
