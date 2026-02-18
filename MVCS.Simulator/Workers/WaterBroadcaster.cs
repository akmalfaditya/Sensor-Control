using MVCS.Simulator.Services;

namespace MVCS.Simulator.Workers;

public class WaterBroadcaster : BackgroundService
{
    private readonly SimulationStateService _state;
    private readonly SimulatorHubClient _hubClient;
    private readonly ILogger<WaterBroadcaster> _logger;
    private readonly Random _random = new();

    public WaterBroadcaster(SimulationStateService state,
        SimulatorHubClient hubClient,
        ILogger<WaterBroadcaster> logger)
    {
        _state = state;
        _hubClient = hubClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WaterBroadcaster started.");

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

                var status = _state.WaterLevel switch
                {
                    >= 80 => "HIGH",
                    >= 20 => "NORMAL",
                    _ => "LOW"
                };

                await _hubClient.PushWaterLevelAsync(Math.Round(_state.WaterLevel, 1), status);
            }

            await Task.Delay(2000, stoppingToken);
        }
    }
}
