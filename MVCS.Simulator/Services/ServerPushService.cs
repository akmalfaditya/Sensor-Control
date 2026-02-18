namespace MVCS.Simulator.Services;

public class ServerPushService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServerPushService> _logger;

    public ServerPushService(IHttpClientFactory httpClientFactory, ILogger<ServerPushService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("MvcsServer");
        _logger = logger;
    }

    public async Task PushCompassAsync(int heading, string cardinal)
    {
        try
        {
            var payload = new { heading, cardinalDirection = cardinal };
            await _httpClient.PostAsJsonAsync("/api/vessel/compass", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to push compass data: {Message}", ex.Message);
        }
    }

    public async Task PushWaterLevelAsync(double level, string status)
    {
        try
        {
            var payload = new { currentLevel = level, status };
            await _httpClient.PostAsJsonAsync("/api/vessel/waterlevel", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to push water level data: {Message}", ex.Message);
        }
    }

    public async Task PushHardwareStateAsync(MVCS.Shared.DTOs.SimulationStateDto state)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/vessel/hardwarestate", state);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to push hardware state: {Message}", ex.Message);
        }
    }
}
