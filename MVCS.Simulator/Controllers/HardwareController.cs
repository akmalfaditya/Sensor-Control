using Microsoft.AspNetCore.Mvc;
using MVCS.Shared.DTOs;
using MVCS.Simulator.Services;

namespace MVCS.Simulator.Controllers;

[ApiController]
[Route("api/hardware")]
public class HardwareController : ControllerBase
{
    private readonly SimulationStateService _state;

    public HardwareController(SimulationStateService state)
    {
        _state = state;
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

        var status = _state.WaterLevel switch
        {
            >= 80 => "HIGH",
            >= 20 => "NORMAL",
            _ => "LOW"
        };

        return Ok(new WaterLevelDto
        {
            CurrentLevel = Math.Round(_state.WaterLevel, 1),
            Status = status
        });
    }

    [HttpPost("pump")]
    public ActionResult<PumpStateDto> SetPump([FromBody] PumpStateDto dto)
    {
        if (!_state.State.IsPumpEnabled)
            return ServiceUnavailable("Pump is disabled");

        _state.PumpIsOn = dto.IsOn;
        return Ok(new PumpStateDto
        {
            IsOn = _state.PumpIsOn,
            Message = _state.PumpIsOn ? "Pump activated" : "Pump deactivated"
        });
    }

    [HttpPost("led")]
    public ActionResult<LedStateDto> SetLed([FromBody] LedStateDto dto)
    {
        if (!_state.State.IsLedEnabled)
            return ServiceUnavailable("LED is disabled");

        _state.LedHexColor = dto.HexColor;
        _state.LedBrightness = dto.Brightness;
        return Ok(new LedStateDto
        {
            HexColor = _state.LedHexColor,
            Brightness = _state.LedBrightness
        });
    }

    private ObjectResult ServiceUnavailable(string message)
    {
        return StatusCode(503, new { error = message, disabled = true });
    }
}
