using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MVCS.Server.Services;
using MVCS.Shared.DTOs;

namespace MVCS.Server.Controllers;

[ApiController]
[Route("api/vessel")]
public class VesselApiController : ControllerBase
{
    private readonly LogService _logService;
    private readonly ServerHubClient _serverHubClient;
    private readonly SimulatorConnectionService _simConn;

    public VesselApiController(LogService logService, ServerHubClient serverHubClient, SimulatorConnectionService simConn)
    {
        _logService = logService;
        _serverHubClient = serverHubClient;
        _simConn = simConn;
    }

    // ---- Commands to Simulator (via Server's outbound SignalR client) ----

    [HttpPost("pump")]
    public async Task<IActionResult> SetPump([FromBody] PumpStateDto dto)
    {
        if (!_serverHubClient.IsConnected)
            return StatusCode(503, new { error = "Simulator offline" });

        try
        {
            var responseJson = await _serverHubClient.SendPumpCommandAsync(dto.IsOn, dto.Message ?? "");

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("error", out _))
                return StatusCode(503, responseJson);

            var result = JsonSerializer.Deserialize<PumpStateDto>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { error = "Simulator command failed", detail = ex.Message });
        }
    }

    [HttpPost("led")]
    public async Task<IActionResult> SetLed([FromBody] LedStateDto dto)
    {
        if (!_serverHubClient.IsConnected)
            return StatusCode(503, new { error = "Simulator offline" });

        try
        {
            var responseJson = await _serverHubClient.SendLedCommandAsync(dto.HexColor, dto.Brightness);

            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("error", out _))
                return StatusCode(503, responseJson);

            var result = JsonSerializer.Deserialize<LedStateDto>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { error = "Simulator command failed", detail = ex.Message });
        }
    }

    [HttpPost("toggle/{component}")]
    public async Task<IActionResult> ToggleHardware(string component)
    {
        var allowed = new[] { "compass", "water", "pump", "led" };
        if (!allowed.Contains(component.ToLower()))
            return BadRequest(new { error = "Invalid component" });

        if (!_serverHubClient.IsConnected)
            return StatusCode(503, new { error = "Simulator offline" });

        try
        {
            var state = await _serverHubClient.SendToggleAsync(component.ToLower());
            return Ok(new { component, toggled = true, state });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new { error = "Simulator command failed", detail = ex.Message });
        }
    }

    // ---- History endpoints (read from local DB) ----

    [HttpGet("history/water")]
    public async Task<IActionResult> GetWaterHistory()
    {
        var data = await _logService.GetWaterHistoryAsync();
        return Ok(data);
    }

    [HttpGet("history/pump")]
    public async Task<IActionResult> GetPumpLogs()
    {
        var data = await _logService.GetPumpLogsAsync();
        return Ok(data);
    }

    [HttpGet("history/compass")]
    public async Task<IActionResult> GetCompassLogs()
    {
        var data = await _logService.GetCompassLogsAsync();
        return Ok(data);
    }

    [HttpGet("history/led")]
    public async Task<IActionResult> GetLedLogs()
    {
        var data = await _logService.GetLedLogsAsync();
        return Ok(data);
    }

    // ---- Simulator State ----

    [HttpGet("simulator/state")]
    public async Task<IActionResult> GetSimulatorState()
    {
        // Try direct query via outbound connection first
        if (_serverHubClient.IsConnected)
        {
            var state = await _serverHubClient.RequestStateAsync();
            if (state != null) return Ok(state);
        }

        // Fallback to cached state from inbound data stream
        if (_simConn.IsSimulatorConnected && _simConn.LastKnownState != null)
            return Ok(_simConn.LastKnownState);

        return Ok(new SimulationStateDto { IsGlobalRunning = false });
    }
}
