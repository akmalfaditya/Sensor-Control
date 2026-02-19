using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MVCS.Shared.DTOs;
using MVCS.Simulator.Hubs;
using MVCS.Simulator.Services;

namespace MVCS.Simulator.Controllers;

[ApiController]
[Route("api/hardware")]
public class HardwareController : ControllerBase
{
    private readonly ISimulationStateService _state;
    private readonly ISimulatorHubClient _hubClient;
    private readonly IHubContext<SimulatorDashboardHub> _dashboardHub;

    public HardwareController(ISimulationStateService state,
        ISimulatorHubClient hubClient,
        IHubContext<SimulatorDashboardHub> dashboardHub)
    {
        _state = state;
        _hubClient = hubClient;
        _dashboardHub = dashboardHub;
    }

    [HttpGet("compass")]
    public ActionResult<CompassDto> GetCompass()
    {
        if (!_state.State.IsCompassEnabled)
            return ServiceUnavailable("Compass is disabled");

        return Ok(new CompassDto
        {
            Heading = _state.CompassHeading,
            CardinalDirection = _state.GetCardinalDirection(_state.CompassHeading)
        });
    }

    [HttpGet("waterlevel")]
    public ActionResult<WaterLevelDto> GetWaterLevel()
    {
        if (!_state.State.IsWaterEnabled)
            return ServiceUnavailable("Water sensor is disabled");

        return Ok(new WaterLevelDto
        {
            CurrentLevel = Math.Round(_state.WaterLevel, 1),
            Status = _state.GetWaterStatus()
        });
    }

    [HttpPost("pump")]
    public async Task<ActionResult<PumpStateDto>> SetPump([FromBody] PumpStateDto dto)
    {
        if (!_state.State.IsPumpEnabled)
            return ServiceUnavailable("Pump is disabled");

        _state.PumpIsOn = dto.IsOn;
        var result = new PumpStateDto
        {
            IsOn = _state.PumpIsOn,
            Message = _state.PumpIsOn ? "Pump activated" : "Pump deactivated"
        };

        // Broadcast to Server via SignalR
        await _hubClient.PushPumpStateAsync(result.IsOn, result.Message);

        // Broadcast to local dashboard
        await _dashboardHub.Clients.All.SendAsync("ReceivePumpState", result.IsOn, result.Message);

        return Ok(result);
    }

    [HttpPost("led")]
    public async Task<ActionResult<LedStateDto>> SetLed([FromBody] LedStateDto dto)
    {
        if (!_state.State.IsLedEnabled)
            return ServiceUnavailable("LED is disabled");

        _state.LedHexColor = dto.HexColor;
        _state.LedBrightness = dto.Brightness;
        var result = new LedStateDto
        {
            HexColor = _state.LedHexColor,
            Brightness = _state.LedBrightness
        };

        // Broadcast to Server via SignalR
        await _hubClient.PushLedStateAsync(result.HexColor, result.Brightness);

        // Broadcast to local dashboard
        await _dashboardHub.Clients.All.SendAsync("ReceiveLedState", result.HexColor, result.Brightness);

        return Ok(result);
    }

    private ObjectResult ServiceUnavailable(string message)
    {
        return StatusCode(503, new { error = message, disabled = true });
    }
}
