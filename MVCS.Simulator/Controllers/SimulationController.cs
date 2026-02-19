using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MVCS.Shared.DTOs;
using MVCS.Simulator.Hubs;
using MVCS.Simulator.Services;

namespace MVCS.Simulator.Controllers;

[ApiController]
[Route("api/simulation")]
public class SimulationController : ControllerBase
{
    private readonly ISimulationStateService _state;
    private readonly ISimulatorHubClient _hubClient;
    private readonly IHubContext<SimulatorDashboardHub> _dashboardHub;

    private static readonly HashSet<string> ValidComponents = new(StringComparer.OrdinalIgnoreCase)
    {
        "compass", "water", "pump", "led"
    };

    public SimulationController(ISimulationStateService state,
        ISimulatorHubClient hubClient,
        IHubContext<SimulatorDashboardHub> dashboardHub)
    {
        _state = state;
        _hubClient = hubClient;
        _dashboardHub = dashboardHub;
    }

    [HttpGet("state")]
    public ActionResult<SimulationStateDto> GetState()
    {
        return Ok(_state.GetStateSnapshot());
    }

    /// <summary>Consolidated toggle endpoint — replaces 4 individual toggle methods.</summary>
    [HttpPost("toggle/{component}")]
    public async Task<IActionResult> Toggle(string component)
    {
        if (!ValidComponents.Contains(component))
            return BadRequest(new { error = $"Invalid component: {component}" });

        _state.Toggle(component);
        await _hubClient.PushHardwareStateAsync();
        await _dashboardHub.Clients.All.SendAsync("ReceiveHardwareState", _state.GetStateSnapshot());

        return Ok(new { component, enabled = _state.GetComponentEnabled(component) });
    }

    [HttpPost("interval/{component}")]
    public async Task<IActionResult> SetInterval(string component, [FromBody] IntervalRequest request)
    {
        _state.SetInterval(component, request.IntervalMs);
        await _hubClient.PushHardwareStateAsync();
        await _dashboardHub.Clients.All.SendAsync("ReceiveHardwareState", _state.GetStateSnapshot());
        return Ok(new
        {
            component,
            intervalMs = component.ToLower() == "compass"
                ? _state.CompassIntervalMs
                : _state.WaterIntervalMs
        });
    }

    public class IntervalRequest
    {
        public int IntervalMs { get; set; }
    }
}
