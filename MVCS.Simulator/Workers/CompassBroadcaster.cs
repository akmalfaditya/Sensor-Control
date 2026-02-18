using MVCS.Simulator.Services;

namespace MVCS.Simulator.Workers;

public class CompassBroadcaster : BackgroundService
{
    private readonly SimulationStateService _state;
    private readonly SimulatorHubClient _hubClient;
    private readonly ILogger<CompassBroadcaster> _logger;
    private readonly Random _random = new();

    public CompassBroadcaster(SimulationStateService state,
        SimulatorHubClient hubClient,
        ILogger<CompassBroadcaster> logger)
    {
        _state = state;
        _hubClient = hubClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CompassBroadcaster started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_state.State.IsGlobalRunning && _state.State.IsCompassEnabled)
            {
                // Simulate compass heading drift
                var drift = _random.Next(-5, 6);
                _state.CompassHeading = (_state.CompassHeading + drift + 360) % 360;
                var cardinal = _state.GetCardinalDirection(_state.CompassHeading);

                await _hubClient.PushCompassAsync(_state.CompassHeading, cardinal);
            }

            await Task.Delay(500, stoppingToken);
        }
    }
}
