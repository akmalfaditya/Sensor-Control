using Microsoft.AspNetCore.SignalR;
using MVCS.Simulator.Hubs;
using MVCS.Simulator.Services;

namespace MVCS.Simulator.Workers;

public class CompassBroadcaster : BackgroundService
{
    private readonly ISimulationStateService _state;
    private readonly ISimulatorHubClient _hubClient;
    private readonly IHubContext<SimulatorDashboardHub> _dashboardHub;
    private readonly ILogger<CompassBroadcaster> _logger;
    private readonly Random _random = new();

    public CompassBroadcaster(ISimulationStateService state,
        ISimulatorHubClient hubClient,
        IHubContext<SimulatorDashboardHub> dashboardHub,
        ILogger<CompassBroadcaster> logger)
    {
        _state = state;
        _hubClient = hubClient;
        _dashboardHub = dashboardHub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CompassBroadcaster started");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_state.State.IsGlobalRunning && _state.State.IsCompassEnabled)
            {
                // Simulate compass heading drift
                var drift = _random.Next(-5, 6);
                _state.CompassHeading = (_state.CompassHeading + drift + 360) % 360;
                var cardinal = _state.GetCardinalDirection(_state.CompassHeading);

                // Push to Server
                await _hubClient.PushCompassAsync(_state.CompassHeading, cardinal);

                // Push to local dashboard
                await _dashboardHub.Clients.All.SendAsync("ReceiveCompass",
                    _state.CompassHeading, cardinal, stoppingToken);
            }

            await Task.Delay(_state.CompassIntervalMs, stoppingToken);
        }
    }
}
