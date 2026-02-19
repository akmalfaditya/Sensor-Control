using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using MVCS.Shared.DTOs;

namespace MVCS.Server.Services;

/// <summary>
/// Manages the SignalR client connection from Server to Simulator's hub.
/// Used by VesselApiController to send commands (pump, LED, toggle).
/// Auto-reconnects independently — Server doesn't depend on Simulator being up.
/// </summary>
public class ServerHubClient : IHostedService, IServerHubClient
{
    private HubConnection? _hub;
    private readonly ILogger<ServerHubClient> _logger;
    private readonly string _simulatorHubUrl;

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);

    public ServerHubClient(ILogger<ServerHubClient> logger, IConfiguration configuration)
    {
        _logger = logger;
        _simulatorHubUrl = configuration["SignalR:SimulatorHubUrl"]
            ?? throw new InvalidOperationException("SignalR:SimulatorHubUrl is not configured in appsettings.json");
    }

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl(_simulatorHubUrl)
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
                _logger.LogInformation("Connected to Simulator SignalR hub at {Url}", _simulatorHubUrl);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to connect to Simulator hub: {Message}. Retrying in 3s...", ex.Message);
                try { await Task.Delay(3000, ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    // ---- Commands to Simulator (return values via InvokeAsync with timeout) ----

    /// <summary>Send pump command. Returns PumpStateDto or error object as JSON.</summary>
    public async Task<string> SendPumpCommandAsync(bool isOn, string message)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Simulator");

        using var cts = new CancellationTokenSource(CommandTimeout);
        var result = await _hub!.InvokeAsync<object>("ExecutePumpCommand", isOn, message, cts.Token);
        return JsonSerializer.Serialize(result);
    }

    /// <summary>Send LED command. Returns LedStateDto or error object as JSON.</summary>
    public async Task<string> SendLedCommandAsync(string hexColor, int brightness)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Simulator");

        using var cts = new CancellationTokenSource(CommandTimeout);
        var result = await _hub!.InvokeAsync<object>("ExecuteLedCommand", hexColor, brightness, cts.Token);
        return JsonSerializer.Serialize(result);
    }

    /// <summary>Toggle a hardware component. Returns updated SimulationStateDto.</summary>
    public async Task<SimulationStateDto?> SendToggleAsync(string component)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Not connected to Simulator");

        using var cts = new CancellationTokenSource(CommandTimeout);
        return await _hub!.InvokeAsync<SimulationStateDto>("ToggleHardware", component, cts.Token);
    }

    /// <summary>Request current simulation state.</summary>
    public async Task<SimulationStateDto?> RequestStateAsync()
    {
        if (!IsConnected)
            return null;

        try
        {
            using var cts = new CancellationTokenSource(CommandTimeout);
            return await _hub!.InvokeAsync<SimulationStateDto>("RequestState", cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request state from Simulator");
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
