using Microsoft.AspNetCore.SignalR;
using MVCS.Simulator.Hubs;
using MVCS.Simulator.Services;

namespace MVCS.Simulator.Workers;

public class WaterBroadcaster : BackgroundService
{
    private readonly ISimulationStateService _state;
    private readonly ISimulatorHubClient _hubClient;
    private readonly IHubContext<SimulatorDashboardHub> _dashboardHub;
    private readonly ILogger<WaterBroadcaster> _logger;
    private readonly Random _random = new();

    public WaterBroadcaster(ISimulationStateService state,
        ISimulatorHubClient hubClient,
        IHubContext<SimulatorDashboardHub> dashboardHub,
        ILogger<WaterBroadcaster> logger)
    {
        _state = state;
        _hubClient = hubClient;
        _dashboardHub = dashboardHub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WaterBroadcaster started");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_state.State.IsGlobalRunning && _state.State.IsWaterEnabled)
            {
                // Simulate water level rising/falling
                var change = _random.NextDouble() * 3.0;

                if (_state.WaterRising)
                {
                    _state.WaterLevel += change;
                    if (_state.WaterLevel >= 95.0)
                    {
                        _state.WaterLevel = 95.0;
                        _state.WaterRising = false;
                    }
                }
                else
                {
                    _state.WaterLevel -= change;
                    if (_state.WaterLevel <= 5.0)
                    {
                        _state.WaterLevel = 5.0;
                        _state.WaterRising = true;
                    }
                }

                // If pump is on, drain faster
                if (_state.PumpIsOn)
                {
                    _state.WaterLevel = Math.Max(0, _state.WaterLevel - 2.0);
                }

                var status = _state.GetWaterStatus();
                var level = Math.Round(_state.WaterLevel, 1);

                // Push to Server
                await _hubClient.PushWaterLevelAsync(level, status);

                // Push to local dashboard
                await _dashboardHub.Clients.All.SendAsync("ReceiveWaterLevel",
                    level, status, stoppingToken);
            }

            await Task.Delay(_state.WaterIntervalMs, stoppingToken);
        }
    }
}
