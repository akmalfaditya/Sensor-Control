using Microsoft.AspNetCore.Mvc;
using MVCS.Shared.DTOs;
using MVCS.Simulator.Services;

namespace MVCS.Simulator.Controllers;

[ApiController]
[Route("api/simulation")]
public class SimulationController : ControllerBase
{
    private readonly SimulationStateService _state;
    private readonly SimulatorHubClient _hubClient;

    public SimulationController(SimulationStateService state, SimulatorHubClient hubClient)
    {
        _state = state;
        _hubClient = hubClient;
    }

    [HttpGet("state")]
    public ActionResult<SimulationStateDto> GetState()
    {
        return Ok(_state.State);
    }

    [HttpPost("toggle/compass")]
    public async Task<IActionResult> ToggleCompass()
    {
        _state.Toggle("compass");
        await _hubClient.PushHardwareStateAsync();
        return Ok(new { component = "compass", enabled = _state.State.IsCompassEnabled });
    }

    [HttpPost("toggle/water")]
    public async Task<IActionResult> ToggleWater()
    {
        _state.Toggle("water");
        await _hubClient.PushHardwareStateAsync();
        return Ok(new { component = "water", enabled = _state.State.IsWaterEnabled });
    }

    [HttpPost("toggle/pump")]
    public async Task<IActionResult> TogglePump()
    {
        _state.Toggle("pump");
        await _hubClient.PushHardwareStateAsync();
        return Ok(new { component = "pump", enabled = _state.State.IsPumpEnabled });
    }

    [HttpPost("toggle/led")]
    public async Task<IActionResult> ToggleLed()
    {
        _state.Toggle("led");
        await _hubClient.PushHardwareStateAsync();
        return Ok(new { component = "led", enabled = _state.State.IsLedEnabled });
    }
}
