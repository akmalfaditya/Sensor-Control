# MVCS — Create from Scratch Guide

Panduan lengkap untuk membuat ulang project **Marine Vessel Control System (MVCS)** dari nol. Ikuti setiap langkah secara berurutan.

---

## Prerequisites

- **.NET 8 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Terminal** (PowerShell / CMD / Bash)
- **Text editor** (VS Code, Visual Studio, dll.)

Verifikasi:
```powershell
dotnet --version
# Harus menampilkan 8.x.x
```

---

## Langkah 1: Buat Solution & Projects

```powershell
# Buat folder project
mkdir "Sensor Control"
cd "Sensor Control"

# Buat solution
dotnet new sln -n MVCS

# Buat 3 project
dotnet new classlib -n MVCS.Shared -f net8.0
dotnet new web -n MVCS.Server -f net8.0
dotnet new webapi -n MVCS.Simulator -f net8.0

# Tambahkan ke solution
dotnet sln add MVCS.Shared
dotnet sln add MVCS.Server
dotnet sln add MVCS.Simulator

# Tambahkan project references
dotnet add MVCS.Server reference MVCS.Shared
dotnet add MVCS.Simulator reference MVCS.Shared
```

---

## Langkah 2: Install NuGet Packages

### MVCS.Server:
```powershell
cd MVCS.Server
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.0.*
dotnet add package Microsoft.AspNetCore.Identity.UI --version 8.0.*
dotnet add package Microsoft.AspNetCore.SignalR.Client --version 8.0.0
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 8.0.*
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 8.0.*
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.*
cd ..
```

### MVCS.Simulator:
```powershell
cd MVCS.Simulator
dotnet add package Microsoft.AspNetCore.SignalR.Client --version 8.0.0
dotnet add package Swashbuckle.AspNetCore --version 6.5.0
cd ..
```

---

## Langkah 3: Hapus File Default

Hapus file-file template yang tidak dipakai:
```powershell
# MVCS.Shared
Remove-Item MVCS.Shared/Class1.cs -ErrorAction SilentlyContinue

# MVCS.Server — hapus default WeatherForecast dll jika ada
Remove-Item MVCS.Server/Controllers/* -ErrorAction SilentlyContinue
Remove-Item MVCS.Server/WeatherForecast.cs -ErrorAction SilentlyContinue

# MVCS.Simulator — hapus default files
Remove-Item MVCS.Simulator/Controllers/* -ErrorAction SilentlyContinue
Remove-Item MVCS.Simulator/WeatherForecast.cs -ErrorAction SilentlyContinue
```

---

## Langkah 4: Buat MVCS.Shared (DTOs)

### `MVCS.Shared/DTOs/CompassDto.cs`
```csharp
namespace MVCS.Shared.DTOs;

public class CompassDto
{
    public int Heading { get; set; }
    public string CardinalDirection { get; set; } = string.Empty;
}
```

### `MVCS.Shared/DTOs/WaterLevelDto.cs`
```csharp
namespace MVCS.Shared.DTOs;

public class WaterLevelDto
{
    public double CurrentLevel { get; set; }
    public string Status { get; set; } = string.Empty;
}
```

### `MVCS.Shared/DTOs/PumpStateDto.cs`
```csharp
namespace MVCS.Shared.DTOs;

public class PumpStateDto
{
    public bool IsOn { get; set; }
    public string Message { get; set; } = string.Empty;
}
```

### `MVCS.Shared/DTOs/LedStateDto.cs`
```csharp
namespace MVCS.Shared.DTOs;

public class LedStateDto
{
    public string HexColor { get; set; } = "#000000";
    public int Brightness { get; set; }
}
```

### `MVCS.Shared/DTOs/SimulationStateDto.cs`
```csharp
namespace MVCS.Shared.DTOs;

public class SimulationStateDto
{
    public bool IsGlobalRunning { get; set; }
    public bool IsCompassEnabled { get; set; } = true;
    public bool IsWaterEnabled { get; set; } = true;
    public bool IsPumpEnabled { get; set; } = true;
    public bool IsLedEnabled { get; set; } = true;
    public int CompassIntervalMs { get; set; } = 500;
    public int WaterIntervalMs { get; set; } = 2000;
}
```

---

## Langkah 5: Buat MVCS.Simulator

### `MVCS.Simulator/Services/SimulationStateService.cs`
```csharp
using MVCS.Shared.DTOs;

namespace MVCS.Simulator.Services;

public class SimulationStateService
{
    private readonly object _lock = new();

    public SimulationStateDto State { get; } = new()
    {
        IsGlobalRunning = true,
        IsCompassEnabled = true,
        IsWaterEnabled = true,
        IsPumpEnabled = true,
        IsLedEnabled = true
    };

    // Current sensor/actuator values
    public int CompassHeading { get; set; } = 0;
    public double WaterLevel { get; set; } = 50.0;
    public bool WaterRising { get; set; } = true;
    public bool PumpIsOn { get; set; } = false;
    public string LedHexColor { get; set; } = "#000000";
    public int LedBrightness { get; set; } = 100;

    // Broadcast intervals (ms)
    public int CompassIntervalMs
    {
        get => State.CompassIntervalMs;
        set => State.CompassIntervalMs = Math.Clamp(value, 100, 10000);
    }

    public int WaterIntervalMs
    {
        get => State.WaterIntervalMs;
        set => State.WaterIntervalMs = Math.Clamp(value, 100, 10000);
    }

    public void SetInterval(string component, int intervalMs)
    {
        lock (_lock)
        {
            switch (component.ToLower())
            {
                case "compass":
                    CompassIntervalMs = intervalMs;
                    break;
                case "water":
                    WaterIntervalMs = intervalMs;
                    break;
            }
        }
    }

    public void Toggle(string component)
    {
        lock (_lock)
        {
            switch (component.ToLower())
            {
                case "compass":
                    State.IsCompassEnabled = !State.IsCompassEnabled;
                    break;
                case "water":
                    State.IsWaterEnabled = !State.IsWaterEnabled;
                    break;
                case "pump":
                    State.IsPumpEnabled = !State.IsPumpEnabled;
                    if (!State.IsPumpEnabled) PumpIsOn = false;
                    break;
                case "led":
                    State.IsLedEnabled = !State.IsLedEnabled;
                    break;
            }
        }
    }

    public string GetCardinalDirection(int heading)
    {
        return heading switch
        {
            >= 337 or < 23 => "N",
            >= 23 and < 68 => "NE",
            >= 68 and < 113 => "E",
            >= 113 and < 158 => "SE",
            >= 158 and < 203 => "S",
            >= 203 and < 248 => "SW",
            >= 248 and < 293 => "W",
            _ => "NW"
        };
    }
}
```

### `MVCS.Simulator/Services/SimulatorHubClient.cs`
```csharp
using Microsoft.AspNetCore.SignalR.Client;
using MVCS.Shared.DTOs;

namespace MVCS.Simulator.Services;

/// <summary>
/// Outbound SignalR client: Simulator → Server's VesselHub (port 5000).
/// Pushes sensor data and state updates to the dashboard.
/// </summary>
public class SimulatorHubClient : IHostedService
{
    private HubConnection? _hub;
    private readonly SimulationStateService _state;
    private readonly ILogger<SimulatorHubClient> _logger;

    public SimulatorHubClient(SimulationStateService state, ILogger<SimulatorHubClient> logger)
    {
        _state = state;
        _logger = logger;
    }

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/vesselhub?role=simulator")
            .WithAutomaticReconnect(new[] {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _hub.Reconnecting += ex =>
        {
            _logger.LogWarning("Reconnecting to Server hub: {Message}", ex?.Message);
            return Task.CompletedTask;
        };

        _hub.Reconnected += connectionId =>
        {
            _logger.LogInformation("Reconnected to Server hub: {ConnectionId}", connectionId);
            _ = PushHardwareStateAsync();
            return Task.CompletedTask;
        };

        _hub.Closed += ex =>
        {
            _logger.LogWarning("Connection to Server hub closed: {Message}", ex?.Message);
            return Task.CompletedTask;
        };

        _ = ConnectWithRetryAsync(cancellationToken);

        return Task.CompletedTask;
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _hub!.StartAsync(ct);
                _logger.LogInformation("Connected to Server SignalR hub at :5000");
                await PushHardwareStateAsync();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to connect to Server hub: {Message}. Retrying in 3s...", ex.Message);
                try { await Task.Delay(3000, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    public async Task PushCompassAsync(int heading, string cardinal)
    {
        if (!IsConnected) return;
        try { await _hub!.InvokeAsync("SimPushCompass", heading, cardinal); }
        catch (Exception ex) { _logger.LogWarning("Failed to push compass: {Message}", ex.Message); }
    }

    public async Task PushWaterLevelAsync(double level, string status)
    {
        if (!IsConnected) return;
        try { await _hub!.InvokeAsync("SimPushWaterLevel", level, status); }
        catch (Exception ex) { _logger.LogWarning("Failed to push water level: {Message}", ex.Message); }
    }

    public async Task PushHardwareStateAsync()
    {
        if (!IsConnected) return;
        try { await _hub!.InvokeAsync("SimPushHardwareState", _state.State); }
        catch (Exception ex) { _logger.LogWarning("Failed to push hardware state: {Message}", ex.Message); }
    }

    public async Task PushPumpStateAsync(bool isOn, string message)
    {
        if (!IsConnected) return;
        try { await _hub!.InvokeAsync("SimPushPumpState", isOn, message); }
        catch (Exception ex) { _logger.LogWarning("Failed to push pump state: {Message}", ex.Message); }
    }

    public async Task PushLedStateAsync(string hexColor, int brightness)
    {
        if (!IsConnected) return;
        try { await _hub!.InvokeAsync("SimPushLedState", hexColor, brightness); }
        catch (Exception ex) { _logger.LogWarning("Failed to push LED state: {Message}", ex.Message); }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_hub != null) await _hub.DisposeAsync();
    }
}
```

### `MVCS.Simulator/Hubs/SimulatorHub.cs`
```csharp
using Microsoft.AspNetCore.SignalR;
using MVCS.Shared.DTOs;
using MVCS.Simulator.Services;

namespace MVCS.Simulator.Hubs;

/// <summary>
/// SignalR hub hosted on the Simulator (port 5100).
/// The Server connects here as a client to send commands.
/// Hub methods return values directly — no correlation IDs needed.
/// Also broadcasts state changes to the local dashboard via SimulatorDashboardHub.
/// </summary>
public class SimulatorHub : Hub
{
    private readonly SimulationStateService _state;
    private readonly SimulatorHubClient _hubClient;
    private readonly IHubContext<SimulatorDashboardHub> _dashboardHub;
    private readonly ILogger<SimulatorHub> _logger;

    public SimulatorHub(SimulationStateService state, SimulatorHubClient hubClient,
        IHubContext<SimulatorDashboardHub> dashboardHub, ILogger<SimulatorHub> logger)
    {
        _state = state;
        _hubClient = hubClient;
        _dashboardHub = dashboardHub;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Server connected to SimulatorHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogWarning("Server disconnected from SimulatorHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // ---- Commands from Server ----

    /// <summary>Server commands the pump on/off. Returns PumpStateDto or error.</summary>
    public async Task<object> ExecutePumpCommand(bool isOn, string message)
    {
        if (!_state.State.IsPumpEnabled)
            return new { error = "Pump is disabled", disabled = true };

        _state.PumpIsOn = isOn;
        var result = new PumpStateDto
        {
            IsOn = _state.PumpIsOn,
            Message = _state.PumpIsOn ? "Pump activated" : "Pump deactivated"
        };

        // Broadcast pump state to Server's dashboard via our outbound connection
        await _hubClient.PushPumpStateAsync(result.IsOn, result.Message);

        // Broadcast to local dashboard
        await _dashboardHub.Clients.All.SendAsync("ReceivePumpState", result.IsOn, result.Message);

        _logger.LogInformation("Pump command executed: IsOn={IsOn}", result.IsOn);
        return result;
    }

    /// <summary>Server commands the LED color/brightness. Returns LedStateDto or error.</summary>
    public async Task<object> ExecuteLedCommand(string hexColor, int brightness)
    {
        if (!_state.State.IsLedEnabled)
            return new { error = "LED is disabled", disabled = true };

        _state.LedHexColor = hexColor;
        _state.LedBrightness = brightness;
        var result = new LedStateDto
        {
            HexColor = _state.LedHexColor,
            Brightness = _state.LedBrightness
        };

        // Broadcast LED state to Server's dashboard via our outbound connection
        await _hubClient.PushLedStateAsync(result.HexColor, result.Brightness);

        // Broadcast to local dashboard
        await _dashboardHub.Clients.All.SendAsync("ReceiveLedState", result.HexColor, result.Brightness);

        _logger.LogInformation("LED command executed: Color={Color}, Brightness={Brightness}", result.HexColor, result.Brightness);
        return result;
    }

    /// <summary>Server asks to toggle a hardware component.</summary>
    public async Task<SimulationStateDto> ToggleHardware(string component)
    {
        _state.Toggle(component);
        _logger.LogInformation("Hardware toggled: {Component}", component);

        // Push updated state to Server's dashboard
        await _hubClient.PushHardwareStateAsync();

        // Push to local dashboard
        await _dashboardHub.Clients.All.SendAsync("ReceiveHardwareState", _state.State);

        return _state.State;
    }

    /// <summary>Server requests current simulation state.</summary>
    public SimulationStateDto RequestState()
    {
        return _state.State;
    }
}
```

### `MVCS.Simulator/Hubs/SimulatorDashboardHub.cs`

> **BARU:** Hub ini khusus untuk browser lokal Simulator. Pisah dari `SimulatorHub` yang menangani koneksi Server.

```csharp
using Microsoft.AspNetCore.SignalR;
using MVCS.Simulator.Services;

namespace MVCS.Simulator.Hubs;

/// <summary>
/// Local SignalR hub for browser clients connecting to the Simulator dashboard UI.
/// Separate from SimulatorHub which handles Server-to-Simulator commands.
/// </summary>
public class SimulatorDashboardHub : Hub
{
    private readonly SimulationStateService _state;
    private readonly ILogger<SimulatorDashboardHub> _logger;

    public SimulatorDashboardHub(SimulationStateService state, ILogger<SimulatorDashboardHub> logger)
    {
        _state = state;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Dashboard browser connected: {ConnectionId}", Context.ConnectionId);

        // Send current state immediately to new client
        await Clients.Caller.SendAsync("ReceiveHardwareState", _state.State);
        await Clients.Caller.SendAsync("ReceiveCompass", _state.CompassHeading,
            _state.GetCardinalDirection(_state.CompassHeading));
        await Clients.Caller.SendAsync("ReceivePumpState", _state.PumpIsOn,
            _state.PumpIsOn ? "Pump is running" : "Pump is idle");
        await Clients.Caller.SendAsync("ReceiveLedState", _state.LedHexColor, _state.LedBrightness);

        var waterStatus = _state.WaterLevel switch
        {
            >= 80 => "HIGH",
            >= 20 => "NORMAL",
            _ => "LOW"
        };
        await Clients.Caller.SendAsync("ReceiveWaterLevel", Math.Round(_state.WaterLevel, 1), waterStatus);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Dashboard browser disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
```

### `MVCS.Simulator/Workers/CompassBroadcaster.cs`
```csharp
using Microsoft.AspNetCore.SignalR;
using MVCS.Simulator.Hubs;
using MVCS.Simulator.Services;

namespace MVCS.Simulator.Workers;

public class CompassBroadcaster : BackgroundService
{
    private readonly SimulationStateService _state;
    private readonly SimulatorHubClient _hubClient;
    private readonly IHubContext<SimulatorDashboardHub> _dashboardHub;
    private readonly ILogger<CompassBroadcaster> _logger;
    private readonly Random _random = new();

    public CompassBroadcaster(SimulationStateService state,
        SimulatorHubClient hubClient,
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
        _logger.LogInformation("CompassBroadcaster started.");

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
```

### `MVCS.Simulator/Workers/WaterBroadcaster.cs`
```csharp
using Microsoft.AspNetCore.SignalR;
using MVCS.Simulator.Hubs;
using MVCS.Simulator.Services;

namespace MVCS.Simulator.Workers;

public class WaterBroadcaster : BackgroundService
{
    private readonly SimulationStateService _state;
    private readonly SimulatorHubClient _hubClient;
    private readonly IHubContext<SimulatorDashboardHub> _dashboardHub;
    private readonly ILogger<WaterBroadcaster> _logger;
    private readonly Random _random = new();

    public WaterBroadcaster(SimulationStateService state,
        SimulatorHubClient hubClient,
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
```

### `MVCS.Simulator/Controllers/HardwareController.cs`
```csharp
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
    private readonly SimulationStateService _state;
    private readonly SimulatorHubClient _hubClient;
    private readonly IHubContext<SimulatorDashboardHub> _dashboardHub;

    public HardwareController(SimulationStateService state,
        SimulatorHubClient hubClient,
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
```

### `MVCS.Simulator/Controllers/SimulationController.cs`
```csharp
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
    private readonly SimulationStateService _state;
    private readonly SimulatorHubClient _hubClient;
    private readonly IHubContext<SimulatorDashboardHub> _dashboardHub;

    public SimulationController(SimulationStateService state,
        SimulatorHubClient hubClient,
        IHubContext<SimulatorDashboardHub> dashboardHub)
    {
        _state = state;
        _hubClient = hubClient;
        _dashboardHub = dashboardHub;
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
        await _dashboardHub.Clients.All.SendAsync("ReceiveHardwareState", _state.State);
        return Ok(new { component = "compass", enabled = _state.State.IsCompassEnabled });
    }

    [HttpPost("toggle/water")]
    public async Task<IActionResult> ToggleWater()
    {
        _state.Toggle("water");
        await _hubClient.PushHardwareStateAsync();
        await _dashboardHub.Clients.All.SendAsync("ReceiveHardwareState", _state.State);
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
```

### `MVCS.Simulator/Program.cs`

> **PENTING:** Simulator sekarang MVC+API hybrid. Gunakan `AddControllersWithViews()` (bukan hanya `AddControllers()`), dan map `SimulatorDashboardHub`.

```csharp
using MVCS.Simulator.Hubs;
using MVCS.Simulator.Services;
using MVCS.Simulator.Workers;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP on port 5100
builder.WebHost.UseUrls("http://localhost:5100");

// Add MVC + Controllers + SignalR server
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MVCS Simulator API", Version = "v1" });
});

// Register services
builder.Services.AddSingleton<SimulationStateService>();
builder.Services.AddSingleton<SimulatorHubClient>();
builder.Services.AddHostedService<SimulatorHubClient>(sp => sp.GetRequiredService<SimulatorHubClient>());

// Register background workers
builder.Services.AddHostedService<CompassBroadcaster>();
builder.Services.AddHostedService<WaterBroadcaster>();

var app = builder.Build();

// Always enable Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MVCS Simulator v1");
    c.RoutePrefix = "swagger";
});

app.UseStaticFiles();
app.UseRouting();

// MVC routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=HomeView}/{action=Index}/{id?}");

// API controllers
app.MapControllers();

// SignalR hubs
app.MapHub<SimulatorHub>("/simulatorhub");
app.MapHub<SimulatorDashboardHub>("/simulatordashboardhub");

app.Run();
```

---

## Langkah 5B: Simulator Dashboard UI (MVC Views)

> **BARU:** Simulator sekarang punya web UI sendiri di `http://localhost:5100` dan dashboard di `/Dashboard`. Ini membutuhkan MVC controllers, Razor views, dan JavaScript.

### `MVCS.Simulator/Controllers/HomeViewController.cs`
```csharp
using Microsoft.AspNetCore.Mvc;

namespace MVCS.Simulator.Controllers;

[Route("")]
public class HomeViewController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }
}
```

### `MVCS.Simulator/Controllers/DashboardViewController.cs`
```csharp
using Microsoft.AspNetCore.Mvc;

namespace MVCS.Simulator.Controllers;

[Route("Dashboard")]
public class DashboardViewController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }
}
```

### `MVCS.Simulator/Views/_ViewImports.cshtml`
```cshtml
@using MVCS.Simulator
@using MVCS.Simulator.Controllers
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

### `MVCS.Simulator/Views/_ViewStart.cshtml`
```cshtml
@{
    Layout = "_Layout";
}
```

### `MVCS.Simulator/Views/Shared/_Layout.cshtml`

> **PENTING:** Sama seperti Server, gunakan `@@keyframes` (double `@`) untuk CSS animations di file `.cshtml`.

```cshtml
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - MVCS Simulator</title>
    <script src="https://cdn.tailwindcss.com"></script>
    <script>
        tailwind.config = {
            darkMode: 'class',
            theme: {
                extend: {
                    colors: {
                        primary: { 600: '#2563eb', 700: '#1d4ed8' }
                    }
                }
            }
        }
    </script>
    <style>
        body { font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; }
        /* Toggle Switch */
        .toggle-switch { position:relative; width:72px; height:36px; display:inline-block; }
        .toggle-switch input { opacity:0; width:0; height:0; }
        .toggle-slider { position:absolute; cursor:pointer; inset:0; background:#334155; border-radius:36px; transition:.3s; box-shadow: inset 0 2px 4px rgba(0,0,0,0.3); }
        .toggle-slider:before { content:''; position:absolute; height:28px; width:28px; left:4px; bottom:4px; background:white; border-radius:50%; transition:.3s; box-shadow: 0 2px 6px rgba(0,0,0,0.3); }
        input:checked + .toggle-slider { background:#22c55e; box-shadow: inset 0 2px 4px rgba(0,0,0,0.2), 0 0 12px rgba(34,197,94,0.3); }
        input:checked + .toggle-slider:before { transform:translateX(36px); }
        /* Water wave animation */
        @@keyframes wave { 0%,100%{transform:translateX(0) translateZ(0) scaleY(1)} 50%{transform:translateX(-25%) translateZ(0) scaleY(0.55)} }
        .water-wave { animation: wave 3s ease-in-out infinite; }
        .water-wave2 { animation: wave 7s ease-in-out infinite; animation-delay: -2s; }
        /* Pulse ring */
        @@keyframes pulseRing { 0%{transform:scale(0.8);opacity:1} 100%{transform:scale(2.2);opacity:0} }
        .pulse-ring { animation: pulseRing 1.5s ease-out infinite; }
        /* Card hover */
        .card-hover { transition: transform 0.25s ease, box-shadow 0.25s ease; }
        .card-hover:hover { transform: translateY(-4px); box-shadow: 0 12px 40px rgba(0,0,0,0.4); }
        /* Glow text */
        @@keyframes glowPulse { 0%,100%{text-shadow:0 0 8px currentColor} 50%{text-shadow:0 0 20px currentColor, 0 0 40px currentColor} }
        .glow-text { animation: glowPulse 2s ease-in-out infinite; }
        /* Brightness slider */
        input[type=range] { -webkit-appearance:none; height:6px; border-radius:3px; background:#334155; outline:none; }
        input[type=range]::-webkit-slider-thumb { -webkit-appearance:none; width:18px; height:18px; border-radius:50%; background:#3b82f6; cursor:pointer; box-shadow: 0 0 6px rgba(59,130,246,0.5); }
        /* LED orb */
        .led-orb { width:80px; height:80px; border-radius:50%; transition: all 0.5s ease; }
        /* Spin animation for pump */
        @@keyframes spin { 0%{transform:rotate(0deg)} 100%{transform:rotate(360deg)} }
        .pump-spin { animation: spin 1s linear infinite; }
        /* Interval badge */
        .interval-badge { font-variant-numeric: tabular-nums; }
    </style>
    @RenderSection("Styles", required: false)
</head>
<body class="bg-slate-900 text-slate-200 min-h-screen">
    @RenderBody()

    <script src="https://cdn.jsdelivr.net/npm/sweetalert2@11"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

### `MVCS.Simulator/Views/HomeView/Index.cshtml`

Landing page untuk Simulator. Menampilkan info dan link ke Dashboard.

```cshtml
@{
    ViewData["Title"] = "Home";
}

<div class="min-h-screen flex flex-col items-center justify-center px-4">
    <div class="text-center max-w-2xl">
        <div class="mb-8">
            <h1 class="text-5xl font-bold text-white mb-2">🔧 MVCS <span class="text-orange-500">Simulator</span></h1>
            <p class="text-slate-400 text-lg">Hardware Simulation Engine</p>
        </div>
        <p class="text-slate-300 text-xl mb-8">
            Simulates marine vessel hardware components — compass, water tank, pump, and deck LED.
            Control hardware state, set broadcast intervals, and monitor real-time data.
        </p>
        <div class="flex flex-col sm:flex-row gap-4 justify-center">
            <a href="/Dashboard"
               class="bg-orange-600 hover:bg-orange-700 text-white font-semibold py-3 px-8 rounded-lg transition-colors text-lg shadow-lg shadow-orange-500/20 hover:shadow-orange-500/40">
                Open Simulator Dashboard
            </a>
            <a href="/swagger"
               class="border border-slate-600 hover:border-slate-400 text-slate-300 hover:text-white font-semibold py-3 px-8 rounded-lg transition-colors text-lg">
                API Docs
            </a>
        </div>
        <div class="mt-16 grid grid-cols-2 md:grid-cols-4 gap-6 text-center">
            <div class="bg-slate-800/50 rounded-lg p-4 border border-slate-700/50">
                <div class="text-3xl mb-2">🧭</div>
                <p class="text-slate-400 text-sm">Compass</p>
                <p class="text-cyan-400 text-xs mt-1 font-mono">500ms</p>
            </div>
            <div class="bg-slate-800/50 rounded-lg p-4 border border-slate-700/50">
                <div class="text-3xl mb-2">💧</div>
                <p class="text-slate-400 text-sm">Water Tank</p>
                <p class="text-blue-400 text-xs mt-1 font-mono">2000ms</p>
            </div>
            <div class="bg-slate-800/50 rounded-lg p-4 border border-slate-700/50">
                <div class="text-3xl mb-2">⚙️</div>
                <p class="text-slate-400 text-sm">Pump Control</p>
                <p class="text-emerald-400 text-xs mt-1 font-mono">On/Off</p>
            </div>
            <div class="bg-slate-800/50 rounded-lg p-4 border border-slate-700/50">
                <div class="text-3xl mb-2">💡</div>
                <p class="text-slate-400 text-sm">Deck LED</p>
                <p class="text-amber-400 text-xs mt-1 font-mono">RGB</p>
            </div>
        </div>
        <div class="mt-8">
            <div class="inline-flex items-center gap-2 bg-slate-800/60 rounded-lg px-4 py-2 border border-slate-700/50">
                <div class="w-2 h-2 rounded-full bg-orange-400 animate-pulse"></div>
                <span class="text-slate-400 text-sm">Port <span class="text-orange-400 font-mono font-bold">5100</span> · Simulator Mode</span>
            </div>
        </div>
    </div>
</div>
```

### `MVCS.Simulator/Views/DashboardView/Index.cshtml`

Dashboard utama Simulator dengan 4 kartu: Compass (canvas gauge), Water Tank (animated), Pump (toggle + spin), LED (color picker + orb). Setiap kartu memiliki interval control (number input + Set button) dan hardware enable/disable overlay.

> **File ini panjang (~228 baris).** Copy dari source project `MVCS.Simulator/Views/DashboardView/Index.cshtml`.

**Fitur utama per kartu:**
- **Compass Card**: Canvas gauge `#compassGauge`, heading text, interval input (100–10,000ms)
- **Water Tank Card**: SVG wave animation, percentage display, status badge, interval input
- **Pump Card**: Toggle switch, spin animation icon, status badge
- **LED Card**: Toggle switch, color picker, brightness slider, LED orb with glow effects

```cshtml
@{
    ViewData["Title"] = "Simulator Dashboard";
}

<!-- Lihat source project untuk isi lengkap: MVCS.Simulator/Views/DashboardView/Index.cshtml -->
<!-- Struktur utama: -->
<!-- NAVBAR → CONNECTION BAR → MAIN (4 CARDS GRID) → Scripts section -->

@section Scripts {
    <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
    <script src="~/js/simulator-dashboard.js" asp-append-version="true"></script>
}
```

### `MVCS.Simulator/wwwroot/js/simulator-dashboard.js`

> **File ini panjang (~528 baris).** Berisi semua logic untuk dashboard simulator.

**Fitur utama:**
1. **SignalR Connection** ke `/simulatordashboardhub` — menerima real-time updates
2. **Canvas Compass** — menggambar compass gauge dengan needle, cardinal marks, tick marks
3. **Water Tank** — update level dan status via SignalR
4. **Pump Control** — toggle via API `/api/hardware/pump`, broadcast ke Server
5. **LED Control** — color picker + brightness via API `/api/hardware/led`, broadcast ke Server
6. **Hardware Toggle** — enable/disable components via API `/api/simulation/toggle/{component}`
7. **Interval Control** — set broadcast interval via API `/api/simulation/interval/{component}` dengan SweetAlert toast feedback

```javascript
// Copy dari source project: MVCS.Simulator/wwwroot/js/simulator-dashboard.js
// Lihat file asli untuk implementasi lengkap.

// Key functions:
// - startConnection() — connect to SimulatorDashboardHub
// - drawCompass(heading) — canvas compass gauge rendering
// - togglePump() — POST /api/hardware/pump + broadcast
// - toggleLed() / setLedColor() — POST /api/hardware/led + broadcast
// - toggleHardware(component) — POST /api/simulation/toggle/{component}
// - setIntervalMs(component, value) — POST /api/simulation/interval/{component}
```

---

## Langkah 6: Buat MVCS.Server

### `MVCS.Server/Models/LogModels.cs`
```csharp
namespace MVCS.Server.Models;

public class CompassLog
{
    public int Id { get; set; }
    public int Heading { get; set; }
    public string Cardinal { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class WaterLevelLog
{
    public int Id { get; set; }
    public double Level { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class PumpLog
{
    public int Id { get; set; }
    public bool IsOn { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class LedLog
{
    public int Id { get; set; }
    public string HexColor { get; set; } = "#000000";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

### `MVCS.Server/Models/AccountViewModels.cs`
```csharp
using System.ComponentModel.DataAnnotations;

namespace MVCS.Server.Models;

public class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}

public class RegisterViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
```

### `MVCS.Server/Models/ErrorViewModel.cs`
```csharp
namespace MVCS.Server.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
```

### `MVCS.Server/Data/ApplicationDbContext.cs`
```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MVCS.Server.Models;

namespace MVCS.Server.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CompassLog> CompassLogs => Set<CompassLog>();
    public DbSet<WaterLevelLog> WaterLevelLogs => Set<WaterLevelLog>();
    public DbSet<PumpLog> PumpLogs => Set<PumpLog>();
    public DbSet<LedLog> LedLogs => Set<LedLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CompassLog>(e => { e.HasIndex(x => x.Timestamp); });
        builder.Entity<WaterLevelLog>(e => { e.HasIndex(x => x.Timestamp); });
        builder.Entity<PumpLog>(e => { e.HasIndex(x => x.Timestamp); });
        builder.Entity<LedLog>(e => { e.HasIndex(x => x.Timestamp); });
    }
}
```

### `MVCS.Server/Services/LogService.cs`
```csharp
using MVCS.Server.Data;
using MVCS.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MVCS.Server.Services;

public class LogService
{
    private readonly ApplicationDbContext _db;

    public LogService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogCompassAsync(int heading, string cardinal)
    {
        _db.CompassLogs.Add(new CompassLog { Heading = heading, Cardinal = cardinal, Timestamp = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }

    public async Task LogWaterLevelAsync(double level, string status)
    {
        _db.WaterLevelLogs.Add(new WaterLevelLog { Level = level, Status = status, Timestamp = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }

    public async Task LogPumpAsync(bool isOn, string message)
    {
        _db.PumpLogs.Add(new PumpLog { IsOn = isOn, Message = message, Timestamp = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }

    public async Task LogLedAsync(string hexColor)
    {
        _db.LedLogs.Add(new LedLog { HexColor = hexColor, Timestamp = DateTime.UtcNow });
        await _db.SaveChangesAsync();
    }

    public async Task<List<WaterLevelLog>> GetWaterHistoryAsync(int count = 50)
    {
        return await _db.WaterLevelLogs.OrderByDescending(x => x.Timestamp).Take(count).OrderBy(x => x.Timestamp).ToListAsync();
    }

    public async Task<List<PumpLog>> GetPumpLogsAsync(int count = 50)
    {
        return await _db.PumpLogs.OrderByDescending(x => x.Timestamp).Take(count).ToListAsync();
    }

    public async Task<List<CompassLog>> GetCompassLogsAsync(int count = 50)
    {
        return await _db.CompassLogs.OrderByDescending(x => x.Timestamp).Take(count).ToListAsync();
    }

    public async Task<List<LedLog>> GetLedLogsAsync(int count = 50)
    {
        return await _db.LedLogs.OrderByDescending(x => x.Timestamp).Take(count).ToListAsync();
    }
}
```

### `MVCS.Server/Services/SimulatorConnectionService.cs`
```csharp
using MVCS.Shared.DTOs;

namespace MVCS.Server.Services;

/// <summary>
/// Tracks the Simulator's inbound SignalR connection to VesselHub (data stream).
/// </summary>
public class SimulatorConnectionService
{
    public string? SimulatorConnectionId { get; set; }
    public bool IsSimulatorConnected => SimulatorConnectionId != null;
    public SimulationStateDto? LastKnownState { get; set; }
}
```

### `MVCS.Server/Services/ServerHubClient.cs`
```csharp
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using MVCS.Shared.DTOs;

namespace MVCS.Server.Services;

/// <summary>
/// Outbound SignalR client: Server → Simulator's SimulatorHub (port 5100).
/// Sends commands (pump, LED, toggle) to Simulator.
/// </summary>
public class ServerHubClient : IHostedService
{
    private HubConnection? _hub;
    private readonly ILogger<ServerHubClient> _logger;

    public ServerHubClient(ILogger<ServerHubClient> logger)
    {
        _logger = logger;
    }

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl("http://localhost:5100/simulatorhub")
            .WithAutomaticReconnect(new[] {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _hub.Reconnecting += ex =>
        {
            _logger.LogWarning("Reconnecting to Simulator hub: {Message}", ex?.Message);
            return Task.CompletedTask;
        };

        _hub.Reconnected += connectionId =>
        {
            _logger.LogInformation("Reconnected to Simulator hub: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _hub.Closed += ex =>
        {
            _logger.LogWarning("Connection to Simulator hub closed: {Message}", ex?.Message);
            return Task.CompletedTask;
        };

        _ = ConnectWithRetryAsync(cancellationToken);

        return Task.CompletedTask;
    }

    private async Task ConnectWithRetryAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _hub!.StartAsync(ct);
                _logger.LogInformation("Connected to Simulator SignalR hub at :5100");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to connect to Simulator hub: {Message}. Retrying in 3s...", ex.Message);
                try { await Task.Delay(3000, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    public async Task<string> SendPumpCommandAsync(bool isOn, string message)
    {
        if (!IsConnected) throw new InvalidOperationException("Not connected to Simulator");
        var result = await _hub!.InvokeAsync<object>("ExecutePumpCommand", isOn, message);
        return JsonSerializer.Serialize(result);
    }

    public async Task<string> SendLedCommandAsync(string hexColor, int brightness)
    {
        if (!IsConnected) throw new InvalidOperationException("Not connected to Simulator");
        var result = await _hub!.InvokeAsync<object>("ExecuteLedCommand", hexColor, brightness);
        return JsonSerializer.Serialize(result);
    }

    public async Task<SimulationStateDto?> SendToggleAsync(string component)
    {
        if (!IsConnected) throw new InvalidOperationException("Not connected to Simulator");
        return await _hub!.InvokeAsync<SimulationStateDto>("ToggleHardware", component);
    }

    public async Task<SimulationStateDto?> RequestStateAsync()
    {
        if (!IsConnected) return null;
        try { return await _hub!.InvokeAsync<SimulationStateDto>("RequestState"); }
        catch (Exception ex) { _logger.LogWarning("Failed to request state: {Message}", ex.Message); return null; }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_hub != null) await _hub.DisposeAsync();
    }
}
```

### `MVCS.Server/Hubs/VesselHub.cs`
```csharp
using Microsoft.AspNetCore.SignalR;
using MVCS.Server.Services;
using MVCS.Shared.DTOs;

namespace MVCS.Server.Hubs;

/// <summary>
/// Inbound SignalR hub: Dashboard JS clients + Simulator connect here.
/// Simulator pushes sensor data, Server broadcasts to Dashboard group.
/// </summary>
public class VesselHub : Hub
{
    private readonly SimulatorConnectionService _simConn;
    private readonly LogService _logService;

    public VesselHub(SimulatorConnectionService simConn, LogService logService)
    {
        _simConn = simConn;
        _logService = logService;
    }

    public override async Task OnConnectedAsync()
    {
        var role = Context.GetHttpContext()?.Request.Query["role"].ToString();
        if (role == "simulator")
        {
            _simConn.SimulatorConnectionId = Context.ConnectionId;
            await Groups.AddToGroupAsync(Context.ConnectionId, "Simulator");

            if (_simConn.LastKnownState != null)
                await Clients.Group("Dashboard").SendAsync("ReceiveHardwareState", _simConn.LastKnownState);
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Dashboard");
            await Clients.Caller.SendAsync("ConnectionStatus", true);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.ConnectionId == _simConn.SimulatorConnectionId)
        {
            _simConn.SimulatorConnectionId = null;
            var offlineState = new SimulationStateDto { IsGlobalRunning = false };
            await Clients.Group("Dashboard").SendAsync("ReceiveHardwareState", offlineState);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SimPushCompass(int heading, string cardinal)
    {
        await _logService.LogCompassAsync(heading, cardinal);
        await Clients.Group("Dashboard").SendAsync("ReceiveCompass", heading, cardinal);
    }

    public async Task SimPushWaterLevel(double level, string status)
    {
        await _logService.LogWaterLevelAsync(level, status);
        await Clients.Group("Dashboard").SendAsync("ReceiveWaterLevel", level, status);
    }

    public async Task SimPushHardwareState(SimulationStateDto state)
    {
        _simConn.LastKnownState = state;
        await Clients.Group("Dashboard").SendAsync("ReceiveHardwareState", state);
    }

    public async Task SimPushPumpState(bool isOn, string message)
    {
        await _logService.LogPumpAsync(isOn, message);
        await Clients.Group("Dashboard").SendAsync("ReceivePumpState", isOn, message);
    }

    public async Task SimPushLedState(string hexColor, int brightness)
    {
        await _logService.LogLedAsync(hexColor);
        await Clients.Group("Dashboard").SendAsync("ReceiveLedState", hexColor, brightness);
    }
}
```

### `MVCS.Server/Controllers/HomeController.cs`
```csharp
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MVCS.Server.Models;

namespace MVCS.Server.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index() => View();

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
```

### `MVCS.Server/Controllers/AccountController.cs`
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MVCS.Server.Models;

namespace MVCS.Server.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;

    public AccountController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
            return LocalRedirect(returnUrl ?? "/Dashboard");

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new IdentityUser { UserName = model.Email, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Dashboard");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login", "Account");
    }
}
```

### `MVCS.Server/Controllers/DashboardController.cs`
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MVCS.Server.Controllers;

[Authorize]
public class DashboardController : Controller
{
    public IActionResult Index() => View();
}
```

### `MVCS.Server/Controllers/VesselApiController.cs`
```csharp
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

    [HttpGet("history/water")]
    public async Task<IActionResult> GetWaterHistory() => Ok(await _logService.GetWaterHistoryAsync());

    [HttpGet("history/pump")]
    public async Task<IActionResult> GetPumpLogs() => Ok(await _logService.GetPumpLogsAsync());

    [HttpGet("history/compass")]
    public async Task<IActionResult> GetCompassLogs() => Ok(await _logService.GetCompassLogsAsync());

    [HttpGet("history/led")]
    public async Task<IActionResult> GetLedLogs() => Ok(await _logService.GetLedLogsAsync());

    [HttpGet("simulator/state")]
    public async Task<IActionResult> GetSimulatorState()
    {
        if (_serverHubClient.IsConnected)
        {
            var state = await _serverHubClient.RequestStateAsync();
            if (state != null) return Ok(state);
        }

        if (_simConn.IsSimulatorConnected && _simConn.LastKnownState != null)
            return Ok(_simConn.LastKnownState);

        return Ok(new SimulationStateDto { IsGlobalRunning = false });
    }
}
```

### `MVCS.Server/Program.cs`
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MVCS.Server.Data;
using MVCS.Server.Hubs;
using MVCS.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP on port 5000
builder.WebHost.UseUrls("http://localhost:5000");

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// SQLite + Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=mvcs.db"));

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});

// Business services
builder.Services.AddScoped<LogService>();
builder.Services.AddSingleton<SimulatorConnectionService>();

// Outbound SignalR client to Simulator's hub (for sending commands)
builder.Services.AddSingleton<ServerHubClient>();
builder.Services.AddHostedService<ServerHubClient>(sp => sp.GetRequiredService<ServerHubClient>());

var app = builder.Build();

// Auto-migrate database & seed admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    // Seed admin user
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    const string adminEmail = "admin@mvcs.com";
    const string adminPassword = "Admin123";

    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new IdentityUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(admin, adminPassword);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<VesselHub>("/vesselhub");

app.Run();
```

---

## Langkah 7: Buat Views (Razor)

### `MVCS.Server/Views/_ViewImports.cshtml`
```cshtml
@using MVCS.Server
@using MVCS.Server.Models
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

### `MVCS.Server/Views/_ViewStart.cshtml`
```cshtml
@{
    Layout = "_Layout";
}
```

### `MVCS.Server/Views/Shared/_Layout.cshtml`

> **PENTING:** Di file Razor `.cshtml`, karakter `@` adalah Razor directive. Untuk menulis `@keyframes` CSS, gunakan `@@keyframes` (double `@`).

```cshtml
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - MVCS Pro</title>
    <script src="https://cdn.tailwindcss.com"></script>
    <script>
        tailwind.config = {
            darkMode: 'class',
            theme: {
                extend: {
                    colors: {
                        primary: { 600: '#2563eb', 700: '#1d4ed8' }
                    }
                }
            }
        }
    </script>
    <style>
        body { font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; }
        .toggle-switch { position:relative; width:72px; height:36px; display:inline-block; }
        .toggle-switch input { opacity:0; width:0; height:0; }
        .toggle-slider { position:absolute; cursor:pointer; inset:0; background:#334155; border-radius:36px; transition:.3s; box-shadow: inset 0 2px 4px rgba(0,0,0,0.3); }
        .toggle-slider:before { content:''; position:absolute; height:28px; width:28px; left:4px; bottom:4px; background:white; border-radius:50%; transition:.3s; box-shadow: 0 2px 6px rgba(0,0,0,0.3); }
        input:checked + .toggle-slider { background:#22c55e; box-shadow: inset 0 2px 4px rgba(0,0,0,0.2), 0 0 12px rgba(34,197,94,0.3); }
        input:checked + .toggle-slider:before { transform:translateX(36px); }
        @@keyframes wave { 0%,100%{transform:translateX(0) translateZ(0) scaleY(1)} 50%{transform:translateX(-25%) translateZ(0) scaleY(0.55)} }
        .water-wave { animation: wave 3s ease-in-out infinite; }
        .water-wave2 { animation: wave 7s ease-in-out infinite; animation-delay: -2s; }
        @@keyframes pulseRing { 0%{transform:scale(0.8);opacity:1} 100%{transform:scale(2.2);opacity:0} }
        .pulse-ring { animation: pulseRing 1.5s ease-out infinite; }
        .card-hover { transition: transform 0.25s ease, box-shadow 0.25s ease; }
        .card-hover:hover { transform: translateY(-4px); box-shadow: 0 12px 40px rgba(0,0,0,0.4); }
        @@keyframes glowPulse { 0%,100%{text-shadow:0 0 8px currentColor} 50%{text-shadow:0 0 20px currentColor, 0 0 40px currentColor} }
        .glow-text { animation: glowPulse 2s ease-in-out infinite; }
        input[type=range] { -webkit-appearance:none; height:6px; border-radius:3px; background:#334155; outline:none; }
        input[type=range]::-webkit-slider-thumb { -webkit-appearance:none; width:18px; height:18px; border-radius:50%; background:#3b82f6; cursor:pointer; box-shadow: 0 0 6px rgba(59,130,246,0.5); }
        .led-orb { width:80px; height:80px; border-radius:50%; transition: all 0.5s ease; }
        @@keyframes spin { 0%{transform:rotate(0deg)} 100%{transform:rotate(360deg)} }
        .pump-spin { animation: spin 1s linear infinite; }
    </style>
    @RenderSection("Styles", required: false)
</head>
<body class="bg-slate-900 text-slate-200 min-h-screen">
    @RenderBody()

    <script src="https://cdn.jsdelivr.net/npm/sweetalert2@11"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

### `MVCS.Server/Views/Home/Index.cshtml`

File ini berisi landing page. Buat file baru dan copy isi dari `Views/Home/Index.cshtml` di source project. Isi utamanya:

```cshtml
@{
    ViewData["Title"] = "Home";
}

<div class="min-h-screen flex flex-col items-center justify-center px-4">
    <div class="text-center max-w-2xl">
        <div class="mb-8">
            <h1 class="text-5xl font-bold text-white mb-2">⚓ MVCS <span class="text-blue-500">Pro</span></h1>
            <p class="text-slate-400 text-lg">Marine Vessel Control System</p>
        </div>
        <p class="text-slate-300 text-xl mb-8">
            Real-time IoT monitoring and control system for marine vessels.
            Manage compass navigation, water tanks, pumps, and deck lighting from a single dashboard.
        </p>
        <div class="flex flex-col sm:flex-row gap-4 justify-center">
            <a asp-controller="Dashboard" asp-action="Index"
               class="bg-blue-600 hover:bg-blue-700 text-white font-semibold py-3 px-8 rounded-lg transition-colors text-lg">
                Open Dashboard
            </a>
            <a asp-controller="Account" asp-action="Login"
               class="border border-slate-600 hover:border-slate-400 text-slate-300 hover:text-white font-semibold py-3 px-8 rounded-lg transition-colors text-lg">
                Sign In
            </a>
        </div>
        <div class="mt-16 grid grid-cols-2 md:grid-cols-4 gap-6 text-center">
            <div class="bg-slate-800/50 rounded-lg p-4"><div class="text-3xl mb-2">🧭</div><p class="text-slate-400 text-sm">Compass</p></div>
            <div class="bg-slate-800/50 rounded-lg p-4"><div class="text-3xl mb-2">💧</div><p class="text-slate-400 text-sm">Water Tank</p></div>
            <div class="bg-slate-800/50 rounded-lg p-4"><div class="text-3xl mb-2">⚙️</div><p class="text-slate-400 text-sm">Pump Control</p></div>
            <div class="bg-slate-800/50 rounded-lg p-4"><div class="text-3xl mb-2">💡</div><p class="text-slate-400 text-sm">Deck LED</p></div>
        </div>
    </div>
</div>
```

### `MVCS.Server/Views/Account/Login.cshtml`

```cshtml
@model MVCS.Server.Models.LoginViewModel
@{
    ViewData["Title"] = "Sign In";
}

<div class="min-h-screen flex items-center justify-center px-4">
    <div class="w-full max-w-md">
        <div class="bg-slate-800 rounded-lg shadow-xl p-8">
            <div class="text-center mb-8">
                <h1 class="text-3xl font-bold text-white">⚓ MVCS <span class="text-blue-500">Pro</span></h1>
                <p class="text-slate-400 mt-2">Sign in to your account</p>
            </div>

            <form asp-action="Login" asp-controller="Account" method="post">
                <input type="hidden" name="returnUrl" value="@ViewData["ReturnUrl"]" />
                <div asp-validation-summary="ModelOnly" class="text-red-400 text-sm mb-4"></div>

                <div class="mb-4">
                    <label asp-for="Email" class="block text-slate-300 text-sm font-medium mb-2">Email Address</label>
                    <input asp-for="Email" type="email" class="w-full px-4 py-3 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent" placeholder="you@example.com" />
                    <span asp-validation-for="Email" class="text-red-400 text-xs mt-1"></span>
                </div>

                <div class="mb-4">
                    <label asp-for="Password" class="block text-slate-300 text-sm font-medium mb-2">Password</label>
                    <input asp-for="Password" type="password" class="w-full px-4 py-3 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent" placeholder="••••••••" />
                    <span asp-validation-for="Password" class="text-red-400 text-xs mt-1"></span>
                </div>

                <div class="flex items-center mb-6">
                    <input asp-for="RememberMe" type="checkbox" class="w-4 h-4 bg-slate-700 border-slate-600 rounded text-blue-600 focus:ring-blue-500" />
                    <label asp-for="RememberMe" class="ml-2 text-sm text-slate-300">Remember me</label>
                </div>

                <button type="submit" class="w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-3 px-4 rounded-lg transition-colors">Sign In</button>
            </form>

            <div class="mt-6 text-center">
                <p class="text-slate-400 text-sm">Don't have an account? <a asp-action="Register" asp-controller="Account" class="text-blue-400 hover:text-blue-300 font-medium">Register</a></p>
            </div>
        </div>
    </div>
</div>
```

### `MVCS.Server/Views/Account/Register.cshtml`

```cshtml
@model MVCS.Server.Models.RegisterViewModel
@{
    ViewData["Title"] = "Register";
}

<div class="min-h-screen flex items-center justify-center px-4">
    <div class="w-full max-w-md">
        <div class="bg-slate-800 rounded-lg shadow-xl p-8">
            <div class="text-center mb-8">
                <h1 class="text-3xl font-bold text-white">⚓ MVCS <span class="text-blue-500">Pro</span></h1>
                <p class="text-slate-400 mt-2">Create a new account</p>
            </div>

            <form asp-action="Register" asp-controller="Account" method="post">
                <div asp-validation-summary="ModelOnly" class="text-red-400 text-sm mb-4"></div>

                <div class="mb-4">
                    <label asp-for="Email" class="block text-slate-300 text-sm font-medium mb-2">Email Address</label>
                    <input asp-for="Email" type="email" class="w-full px-4 py-3 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent" placeholder="you@example.com" />
                    <span asp-validation-for="Email" class="text-red-400 text-xs mt-1"></span>
                </div>

                <div class="mb-4">
                    <label asp-for="Password" class="block text-slate-300 text-sm font-medium mb-2">Password</label>
                    <input asp-for="Password" type="password" class="w-full px-4 py-3 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent" placeholder="Min. 6 characters" />
                    <span asp-validation-for="Password" class="text-red-400 text-xs mt-1"></span>
                </div>

                <div class="mb-6">
                    <label asp-for="ConfirmPassword" class="block text-slate-300 text-sm font-medium mb-2">Confirm Password</label>
                    <input asp-for="ConfirmPassword" type="password" class="w-full px-4 py-3 bg-slate-700 border border-slate-600 rounded-lg text-white placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent" placeholder="••••••••" />
                    <span asp-validation-for="ConfirmPassword" class="text-red-400 text-xs mt-1"></span>
                </div>

                <button type="submit" class="w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-3 px-4 rounded-lg transition-colors">Create Account</button>
            </form>

            <div class="mt-6 text-center">
                <p class="text-slate-400 text-sm">Already have an account? <a asp-action="Login" asp-controller="Account" class="text-blue-400 hover:text-blue-300 font-medium">Sign In</a></p>
            </div>
        </div>
    </div>
</div>
```

### `MVCS.Server/Views/Dashboard/Index.cshtml`

```cshtml
@{
    ViewData["Title"] = "Dashboard";
}

<!-- NAVBAR -->
<nav class="bg-slate-800/90 backdrop-blur-md border-b border-slate-700/50 px-6 py-3 sticky top-0 z-40">
    <div class="flex items-center justify-between max-w-7xl mx-auto">
        <div class="flex items-center space-x-4">
            <h1 class="text-xl font-bold text-white tracking-tight">⚓ MVCS <span class="text-blue-400">Pro</span></h1>
            <div class="hidden sm:block h-6 w-px bg-slate-600"></div>
            <span id="simulatorBadge" class="px-3 py-1 rounded-full text-xs font-semibold bg-slate-700 text-slate-400 transition-all duration-300">
                Checking...
            </span>
        </div>
        <div class="flex items-center space-x-3">
            <div class="hidden sm:flex items-center space-x-2 bg-slate-700/50 rounded-lg px-3 py-1.5">
                <div class="w-2 h-2 rounded-full bg-green-400"></div>
                <span class="text-slate-300 text-sm">@@User.Identity?.Name</span>
            </div>
            <form asp-controller="Account" asp-action="Logout" method="post" class="inline">
                <button type="submit"
                        class="bg-rose-600/80 hover:bg-rose-600 text-white text-sm font-medium py-2 px-4 rounded-lg transition-all duration-200 hover:shadow-lg hover:shadow-rose-500/20">
                    Logout
                </button>
            </form>
        </div>
    </div>
</nav>

<!-- CONNECTION STATUS BAR -->
<div id="connectionBar" class="h-1 bg-red-500 transition-all duration-500 shadow-sm"></div>

<!-- MAIN CONTENT -->
<main class="max-w-7xl mx-auto p-6">
    <!-- Section Header -->
    <div class="mb-6 flex items-center justify-between">
        <div>
            <h2 class="text-2xl font-bold text-white">Control Dashboard</h2>
            <p class="text-slate-400 text-sm mt-1">Real-time vessel monitoring & control</p>
        </div>
        <div id="clockDisplay" class="text-slate-400 text-sm font-mono bg-slate-800/60 rounded-lg px-3 py-2 border border-slate-700/50"></div>
    </div>

    <!-- 4 CARDS GRID -->
    <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-6">

        <!-- CARD 1: COMPASS -->
        <div id="compassCard" class="card-hover bg-gradient-to-br from-slate-800 to-slate-800/80 rounded-2xl border border-slate-700/50 shadow-xl overflow-hidden relative">
            <!-- Disabled Overlay -->
            <div id="compassOverlay" class="absolute inset-0 bg-slate-900/70 backdrop-blur-[2px] z-20 hidden flex-col items-center justify-center">
                <svg id="compassOverlayIcon" class="w-10 h-10 text-red-400 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636"/></svg>
                <span id="compassOverlayText" class="text-red-400 font-bold text-sm">HARDWARE DISABLED</span>
                <button id="compassOverlayBtn" onclick="toggleHardware('compass')" class="mt-3 px-4 py-1.5 rounded-lg bg-green-600/80 hover:bg-green-600 text-white text-xs font-bold transition-all hover:shadow-lg hover:shadow-green-500/20">Enable</button>
            </div>
            <div class="px-5 py-3 border-b border-slate-700/50 flex items-center justify-between">
                <h3 class="text-sm font-semibold text-slate-300 uppercase tracking-wider flex items-center gap-2">
                    <span class="w-2 h-2 rounded-full bg-cyan-400 inline-block"></span>
                    Compass Heading
                </h3>
                <div class="flex items-center gap-2">
                    <button id="compassHwBadge" onclick="toggleHardware('compass')" class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300 cursor-pointer hover:scale-105">HW ON</button>
                    <span id="compassLive" class="w-2 h-2 rounded-full bg-green-400 animate-pulse"></span>
                </div>
            </div>
            <div class="p-5 flex flex-col items-center justify-center" style="min-height: 240px;">
                <canvas id="compassGauge" width="200" height="200"></canvas>
            </div>
            <div class="px-5 py-3 border-t border-slate-700/50 flex items-center justify-between relative z-30 bg-gradient-to-br from-slate-800 to-slate-800/80">
                <span id="compassText" class="text-xl font-mono font-bold text-cyan-400 glow-text">N 0°</span>
                <button onclick="openCompassLogs()" class="inline-flex items-center gap-1.5 text-sm text-cyan-400 hover:text-cyan-300 transition-colors hover:underline underline-offset-4">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v10a2 2 0 002 2h8a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/></svg>
                    Log
                </button>
            </div>
        </div>

        <!-- CARD 2: WATER TANK -->
        <div id="waterCard" class="card-hover bg-gradient-to-br from-slate-800 to-slate-800/80 rounded-2xl border border-slate-700/50 shadow-xl overflow-hidden relative">
            <!-- Disabled Overlay -->
            <div id="waterOverlay" class="absolute inset-0 bg-slate-900/70 backdrop-blur-[2px] z-20 hidden flex-col items-center justify-center">
                <svg id="waterOverlayIcon" class="w-10 h-10 text-red-400 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636"/></svg>
                <span id="waterOverlayText" class="text-red-400 font-bold text-sm">HARDWARE DISABLED</span>
                <button id="waterOverlayBtn" onclick="toggleHardware('water')" class="mt-3 px-4 py-1.5 rounded-lg bg-green-600/80 hover:bg-green-600 text-white text-xs font-bold transition-all hover:shadow-lg hover:shadow-green-500/20">Enable</button>
            </div>
            <div class="px-5 py-3 border-b border-slate-700/50 flex items-center justify-between">
                <h3 class="text-sm font-semibold text-slate-300 uppercase tracking-wider flex items-center gap-2">
                    <span class="w-2 h-2 rounded-full bg-blue-400 inline-block"></span>
                    Water Level
                </h3>
                <div class="flex items-center gap-2">
                    <button id="waterHwBadge" onclick="toggleHardware('water')" class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300 cursor-pointer hover:scale-105">HW ON</button>
                    <span id="waterStatus" class="px-2.5 py-0.5 rounded-full text-xs font-bold bg-green-500/20 text-green-400 transition-all">NORMAL</span>
                </div>
            </div>
            <div class="p-5 flex flex-col items-center justify-center" style="min-height: 240px;">
                <div class="relative w-28 rounded-xl overflow-hidden border-2 border-slate-600/50" style="height: 190px; background: linear-gradient(to bottom, #0f172a, #1e293b);">
                    <!-- Wave layers -->
                    <div id="waterFill" class="absolute bottom-0 w-full transition-all duration-1000 ease-out" style="height: 50%;">
                        <svg class="water-wave absolute top-0 left-0 w-[200%]" viewBox="0 0 400 20" style="margin-top:-8px">
                            <path d="M0,10 C100,0 200,20 400,10 L400,20 L0,20 Z" fill="rgba(59,130,246,0.6)"/>
                        </svg>
                        <svg class="water-wave2 absolute top-0 left-0 w-[200%]" viewBox="0 0 400 20" style="margin-top:-4px">
                            <path d="M0,10 C80,20 160,0 240,10 C320,20 400,0 400,10 L400,20 L0,20 Z" fill="rgba(59,130,246,0.3)"/>
                        </svg>
                        <div class="absolute inset-0 bg-blue-500/40" style="margin-top:10px"></div>
                    </div>
                    <div class="absolute inset-0 flex items-center justify-center z-10">
                        <span id="waterPercent" class="text-white font-bold text-2xl drop-shadow-lg">50%</span>
                    </div>
                    <!-- Level markers -->
                    <div class="absolute right-1 top-[10%] text-slate-500 text-[9px] font-mono">90</div>
                    <div class="absolute right-1 top-[30%] text-slate-500 text-[9px] font-mono">70</div>
                    <div class="absolute right-1 top-[50%] text-slate-500 text-[9px] font-mono">50</div>
                    <div class="absolute right-1 top-[70%] text-slate-500 text-[9px] font-mono">30</div>
                    <div class="absolute right-1 top-[90%] text-slate-500 text-[9px] font-mono">10</div>
                </div>
            </div>
            <div class="px-5 py-3 border-t border-slate-700/50 text-center relative z-30 bg-gradient-to-br from-slate-800 to-slate-800/80">
                <button onclick="openWaterHistory()" class="inline-flex items-center gap-1.5 text-sm text-blue-400 hover:text-blue-300 transition-colors hover:underline underline-offset-4">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h4a2 2 0 012 2v10m-6 0h6m2 0v-5a2 2 0 012-2h2a2 2 0 012 2v5a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/></svg>
                    View History
                </button>
            </div>
        </div>

        <!-- CARD 3: PUMP CONTROL -->
        <div id="pumpCard" class="card-hover bg-gradient-to-br from-slate-800 to-slate-800/80 rounded-2xl border border-slate-700/50 shadow-xl overflow-hidden relative">
            <!-- Disabled Overlay -->
            <div id="pumpOverlay" class="absolute inset-0 bg-slate-900/70 backdrop-blur-[2px] z-20 hidden flex-col items-center justify-center">
                <svg id="pumpOverlayIcon" class="w-10 h-10 text-red-400 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636"/></svg>
                <span id="pumpOverlayText" class="text-red-400 font-bold text-sm">HARDWARE DISABLED</span>
                <button id="pumpOverlayBtn" onclick="toggleHardware('pump')" class="mt-3 px-4 py-1.5 rounded-lg bg-green-600/80 hover:bg-green-600 text-white text-xs font-bold transition-all hover:shadow-lg hover:shadow-green-500/20">Enable</button>
            </div>
            <div class="px-5 py-3 border-b border-slate-700/50 flex items-center justify-between">
                <h3 class="text-sm font-semibold text-slate-300 uppercase tracking-wider flex items-center gap-2">
                    <span class="w-2 h-2 rounded-full bg-emerald-400 inline-block"></span>
                    Main Pump
                </h3>
                <div class="flex items-center gap-2">
                    <button id="pumpHwBadge" onclick="toggleHardware('pump')" class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300 cursor-pointer hover:scale-105">HW ON</button>
                    <span id="pumpBadge" class="px-2.5 py-0.5 rounded-full text-xs font-bold bg-slate-600/50 text-slate-400 transition-all">OFF</span>
                </div>
            </div>
            <div class="p-5 flex flex-col items-center justify-center" style="min-height: 240px;">
                <!-- Pump icon with spin animation -->
                <div class="relative mb-4">
                    <div id="pumpRing" class="absolute inset-0 rounded-full border-2 border-transparent"></div>
                    <div id="pumpIcon" class="w-16 h-16 rounded-full bg-slate-700 flex items-center justify-center text-3xl transition-all duration-300">
                        ⚙️
                    </div>
                </div>
                <label class="toggle-switch mb-3">
                    <input type="checkbox" id="pumpToggle" onchange="togglePump()" />
                    <span class="toggle-slider"></span>
                </label>
                <p id="pumpStatus" class="text-slate-500 text-sm font-medium">Idle</p>
            </div>
            <div class="px-5 py-3 border-t border-slate-700/50 text-center relative z-30 bg-gradient-to-br from-slate-800 to-slate-800/80">
                <button onclick="openPumpLogs()" class="inline-flex items-center gap-1.5 text-sm text-blue-400 hover:text-blue-300 transition-colors hover:underline underline-offset-4">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v10a2 2 0 002 2h8a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/></svg>
                    Activity Log
                </button>
            </div>
        </div>

        <!-- CARD 4: DECK LED -->
        <div id="ledCard" class="card-hover bg-gradient-to-br from-slate-800 to-slate-800/80 rounded-2xl border border-slate-700/50 shadow-xl overflow-hidden relative">
            <!-- Disabled Overlay -->
            <div id="ledOverlay" class="absolute inset-0 bg-slate-900/70 backdrop-blur-[2px] z-20 hidden flex-col items-center justify-center">
                <svg id="ledOverlayIcon" class="w-10 h-10 text-red-400 mb-2" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M18.364 18.364A9 9 0 005.636 5.636m12.728 12.728A9 9 0 015.636 5.636m12.728 12.728L5.636 5.636"/></svg>
                <span id="ledOverlayText" class="text-red-400 font-bold text-sm">HARDWARE DISABLED</span>
                <button id="ledOverlayBtn" onclick="toggleHardware('led')" class="mt-3 px-4 py-1.5 rounded-lg bg-green-600/80 hover:bg-green-600 text-white text-xs font-bold transition-all hover:shadow-lg hover:shadow-green-500/20">Enable</button>
            </div>
            <div class="px-5 py-3 border-b border-slate-700/50 flex items-center justify-between">
                <h3 class="text-sm font-semibold text-slate-300 uppercase tracking-wider flex items-center gap-2">
                    <span id="ledDot" class="w-2 h-2 rounded-full bg-slate-500 inline-block transition-all duration-300"></span>
                    Deck LED
                </h3>
                <div class="flex items-center gap-2">
                    <button id="ledHwBadge" onclick="toggleHardware('led')" class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300 cursor-pointer hover:scale-105">HW ON</button>
                    <span id="ledBadge" class="px-2.5 py-0.5 rounded-full text-xs font-bold bg-slate-600/50 text-slate-400 transition-all">OFF</span>
                </div>
            </div>
            <div class="p-5 flex flex-col items-center justify-center" style="min-height: 240px;">
                <!-- LED Orb Preview -->
                <div class="relative mb-4">
                    <div id="ledOrb" class="led-orb bg-slate-700 border-2 border-slate-600 flex items-center justify-center">
                        <span class="text-3xl">💡</span>
                    </div>
                    <div id="ledPulse" class="absolute inset-0 rounded-full border-2 border-transparent"></div>
                </div>
                <!-- LED Toggle -->
                <label class="toggle-switch mb-3">
                    <input type="checkbox" id="ledToggle" onchange="toggleLed()" />
                    <span class="toggle-slider"></span>
                </label>
                <!-- Color & Brightness controls (hidden when OFF) -->
                <div id="ledControls" class="w-full space-y-3 opacity-30 pointer-events-none transition-all duration-300">
                    <input type="color" id="ledColorPicker" value="#3b82f6"
                           onchange="setLedColor(this.value)"
                           class="w-full h-10 rounded-lg border border-slate-600 cursor-pointer bg-transparent" />
                    <div class="flex items-center gap-2">
                        <svg class="w-4 h-4 text-slate-500 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20"><path d="M10 2a6 6 0 00-6 6v3.586l-.707.707A1 1 0 004 14h12a1 1 0 00.707-1.707L16 11.586V8a6 6 0 00-6-6z"/></svg>
                        <input type="range" id="ledBrightness" min="0" max="100" value="100"
                               oninput="document.getElementById('brightnessVal').textContent=this.value+'%'; setLedColor(document.getElementById('ledColorPicker').value)"
                               class="flex-1" />
                        <span id="brightnessVal" class="text-slate-400 text-xs w-10 text-right font-mono">100%</span>
                    </div>
                </div>
            </div>
            <div class="px-5 py-3 border-t border-slate-700/50 flex items-center justify-between relative z-30 bg-gradient-to-br from-slate-800 to-slate-800/80">
                <p id="ledHexText" class="text-slate-500 text-xs font-mono">#000000</p>
                <button onclick="openLedLogs()" class="inline-flex items-center gap-1.5 text-sm text-amber-400 hover:text-amber-300 transition-colors hover:underline underline-offset-4">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v10a2 2 0 002 2h8a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/></svg>
                    Log
                </button>
            </div>
        </div>

    </div>
</main>

<!-- MODAL: COMPASS LOGS -->
<div id="compassLogModal" class="fixed inset-0 z-50 hidden items-center justify-center">
    <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" onclick="closeModal('compassLogModal')"></div>
    <div class="relative bg-gradient-to-br from-slate-800 to-slate-900 rounded-2xl shadow-2xl w-full max-w-3xl mx-4 max-h-[85vh] overflow-auto border border-slate-700/50">
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-700/50">
            <h2 class="text-lg font-bold text-white flex items-center gap-2">
                <svg class="w-5 h-5 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7"/></svg>
                Compass Heading Log
            </h2>
            <button onclick="closeModal('compassLogModal')" class="w-8 h-8 flex items-center justify-center rounded-lg bg-slate-700/50 hover:bg-slate-600 text-slate-400 hover:text-white transition-colors">&times;</button>
        </div>
        <div class="p-6">
            <table class="w-full text-sm">
                <thead>
                    <tr class="text-left text-slate-400 border-b border-slate-700">
                        <th class="pb-3 pr-4 font-medium">Timestamp</th>
                        <th class="pb-3 pr-4 font-medium">Heading</th>
                        <th class="pb-3 font-medium">Direction</th>
                    </tr>
                </thead>
                <tbody id="compassLogBody" class="text-slate-300">
                    <tr><td colspan="3" class="py-8 text-center text-slate-500">Loading...</td></tr>
                </tbody>
            </table>
        </div>
    </div>
</div>

<!-- MODAL: WATER HISTORY -->
<div id="waterHistoryModal" class="fixed inset-0 z-50 hidden items-center justify-center">
    <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" onclick="closeModal('waterHistoryModal')"></div>
    <div class="relative bg-gradient-to-br from-slate-800 to-slate-900 rounded-2xl shadow-2xl w-full max-w-3xl mx-4 max-h-[85vh] overflow-auto border border-slate-700/50">
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-700/50">
            <h2 class="text-lg font-bold text-white flex items-center gap-2">
                <svg class="w-5 h-5 text-blue-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h4a2 2 0 012 2v10m-6 0h6m2 0v-5a2 2 0 012-2h2a2 2 0 012 2v5a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/></svg>
                Water Level History
            </h2>
            <button onclick="closeModal('waterHistoryModal')" class="w-8 h-8 flex items-center justify-center rounded-lg bg-slate-700/50 hover:bg-slate-600 text-slate-400 hover:text-white transition-colors">&times;</button>
        </div>
        <div class="p-6">
            <canvas id="waterChart" height="300"></canvas>
        </div>
    </div>
</div>

<!-- MODAL: PUMP LOGS -->
<div id="pumpLogModal" class="fixed inset-0 z-50 hidden items-center justify-center">
    <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" onclick="closeModal('pumpLogModal')"></div>
    <div class="relative bg-gradient-to-br from-slate-800 to-slate-900 rounded-2xl shadow-2xl w-full max-w-3xl mx-4 max-h-[85vh] overflow-auto border border-slate-700/50">
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-700/50">
            <h2 class="text-lg font-bold text-white flex items-center gap-2">
                <svg class="w-5 h-5 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v10a2 2 0 002 2h8a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/></svg>
                Pump Activity Log
            </h2>
            <button onclick="closeModal('pumpLogModal')" class="w-8 h-8 flex items-center justify-center rounded-lg bg-slate-700/50 hover:bg-slate-600 text-slate-400 hover:text-white transition-colors">&times;</button>
        </div>
        <div class="p-6">
            <table class="w-full text-sm">
                <thead>
                    <tr class="text-left text-slate-400 border-b border-slate-700">
                        <th class="pb-3 pr-4 font-medium">Timestamp</th>
                        <th class="pb-3 pr-4 font-medium">State</th>
                        <th class="pb-3 font-medium">Message</th>
                    </tr>
                </thead>
                <tbody id="pumpLogBody" class="text-slate-300">
                    <tr><td colspan="3" class="py-8 text-center text-slate-500">Loading...</td></tr>
                </tbody>
            </table>
        </div>
    </div>
</div>

<!-- MODAL: LED LOGS -->
<div id="ledLogModal" class="fixed inset-0 z-50 hidden items-center justify-center">
    <div class="absolute inset-0 bg-black/60 backdrop-blur-sm" onclick="closeModal('ledLogModal')"></div>
    <div class="relative bg-gradient-to-br from-slate-800 to-slate-900 rounded-2xl shadow-2xl w-full max-w-3xl mx-4 max-h-[85vh] overflow-auto border border-slate-700/50">
        <div class="flex items-center justify-between px-6 py-4 border-b border-slate-700/50">
            <h2 class="text-lg font-bold text-white flex items-center gap-2">
                <svg class="w-5 h-5 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z"/></svg>
                LED Color Log
            </h2>
            <button onclick="closeModal('ledLogModal')" class="w-8 h-8 flex items-center justify-center rounded-lg bg-slate-700/50 hover:bg-slate-600 text-slate-400 hover:text-white transition-colors">&times;</button>
        </div>
        <div class="p-6">
            <table class="w-full text-sm">
                <thead>
                    <tr class="text-left text-slate-400 border-b border-slate-700">
                        <th class="pb-3 pr-4 font-medium">Timestamp</th>
                        <th class="pb-3 font-medium">Color</th>
                    </tr>
                </thead>
                <tbody id="ledLogBody" class="text-slate-300">
                    <tr><td colspan="2" class="py-8 text-center text-slate-500">Loading...</td></tr>
                </tbody>
            </table>
        </div>
    </div>
</div>

@@section Scripts {
    <script src="https://cdn.jsdelivr.net/npm/chart.js@@4"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.0/signalr.min.js"></script>
    <script src="~/js/dashboard.js" asp-append-version="true"></script>
}
```

> **PENTING:** Di Razor `.cshtml`, karakter `@` harus di-escape menjadi `@@` untuk penggunaan non-Razor (misalnya `@@section`, `@@User` di atas). Saat file ini dibuat sebagai `.cshtml` yang sebenarnya, gunakan single `@` kecuali di dalam CSS/JS literal yang membutuhkan `@@` (seperti `@@keyframes` di Layout).

---

## Langkah 8: Buat Static Files

### `MVCS.Server/wwwroot/css/site.css`
```css
html {
  font-size: 14px;
}

@media (min-width: 768px) {
  html {
    font-size: 16px;
  }
}

.btn:focus, .btn:active:focus, .btn-link.nav-link:focus, .form-control:focus, .form-check-input:focus {
  box-shadow: 0 0 0 0.1rem white, 0 0 0 0.25rem #258cfb;
}

html {
  position: relative;
  min-height: 100%;
}

body {
  margin-bottom: 60px;
}
```

### `MVCS.Server/wwwroot/js/dashboard.js`

```javascript
// =============================================
// MVCS Pro - Dashboard JavaScript v2
// Enhanced UI: Animations, LED Toggle, Better Compass
// =============================================

// ---- State ----
let currentHeading = 0;
let currentCardinal = 'N';
let waterChart = null;
let ledIsOn = false;
let lastSimulatorUpdate = Date.now();

// ---- Live Clock ----
function updateClock() {
    const el = document.getElementById('clockDisplay');
    if (el) {
        const now = new Date();
        el.textContent = now.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
            + '  ' + now.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
    }
}
setInterval(updateClock, 1000);
updateClock();

// ---- SignalR Connection ----
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/vesselhub")
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();

connection.onreconnecting(() => setConnectionStatus(false));
connection.onreconnected(() => setConnectionStatus(true));
connection.onclose(() => setConnectionStatus(false));

function setConnectionStatus(connected) {
    const bar = document.getElementById('connectionBar');
    bar.className = connected
        ? 'h-1 bg-green-500 transition-all duration-500 shadow-sm shadow-green-500/50'
        : 'h-1 bg-red-500 transition-all duration-500 shadow-sm shadow-red-500/50 animate-pulse';
}

// ---- SignalR Handlers ----
connection.on("ConnectionStatus", (status) => setConnectionStatus(status));

connection.on("ReceiveCompass", (heading, cardinal) => {
    lastSimulatorUpdate = Date.now();
    currentHeading = heading;
    currentCardinal = cardinal;
    document.getElementById('compassText').textContent = `${cardinal} ${heading}°`;
    drawCompass(heading);
});

connection.on("ReceiveWaterLevel", (level, status) => {
    lastSimulatorUpdate = Date.now();
    const fill = document.getElementById('waterFill');
    const percent = document.getElementById('waterPercent');
    const statusEl = document.getElementById('waterStatus');

    fill.style.height = `${level}%`;
    percent.textContent = `${Math.round(level)}%`;

    const cfg = {
        'HIGH':   { bg: 'bg-red-500/20',    text: 'text-red-400',    border: '' },
        'NORMAL': { bg: 'bg-green-500/20',   text: 'text-green-400',  border: '' },
        'LOW':    { bg: 'bg-yellow-500/20',  text: 'text-yellow-400', border: '' }
    };
    const c = cfg[status] || cfg['NORMAL'];
    statusEl.className = `px-2.5 py-0.5 rounded-full text-xs font-bold ${c.bg} ${c.text} transition-all`;
    statusEl.textContent = status;
});

connection.on("ReceivePumpState", (isOn, message) => {
    lastSimulatorUpdate = Date.now();
    const toggle = document.getElementById('pumpToggle');
    toggle.checked = isOn;
    updatePumpVisual(isOn);
});

connection.on("ReceiveLedState", (hexColor, brightness) => {
    lastSimulatorUpdate = Date.now();
    if (ledIsOn) updateLedVisual(hexColor, brightness);
});

connection.on("ReceiveHardwareState", (state) => {
    lastSimulatorUpdate = Date.now();
    const badge = document.getElementById('simulatorBadge');
    if (state.isGlobalRunning) {
        badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300';
        badge.textContent = '● Running';
    } else {
        badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-yellow-500/15 text-yellow-400 border border-yellow-500/30 transition-all duration-300';
        badge.textContent = '● Standby';
    }
    simulatorOnline = true;
    setHwBadge('compassHwBadge', 'compassOverlay', state.isCompassEnabled, false);
    setHwBadge('waterHwBadge', 'waterOverlay', state.isWaterEnabled, false);
    setHwBadge('pumpHwBadge', 'pumpOverlay', state.isPumpEnabled, false);
    setHwBadge('ledHwBadge', 'ledOverlay', state.isLedEnabled, false);
});

// ---- Start Connection ----
async function startConnection() {
    try {
        await connection.start();
        setConnectionStatus(true);
        checkSimulatorState();
    } catch (err) {
        console.error("SignalR error:", err);
        setConnectionStatus(false);
        setTimeout(startConnection, 5000);
    }
}

// ---- Simulator State ----
let simulatorOnline = true;

function setHwBadge(badgeId, overlayId, enabled, offline) {
    const badge = document.getElementById(badgeId);
    const overlay = document.getElementById(overlayId);
    const component = overlayId.replace('Overlay', '');
    const overlayText = document.getElementById(component + 'OverlayText');
    const overlayBtn = document.getElementById(component + 'OverlayBtn');

    if (badge) {
        if (offline) {
            badge.className = 'px-2 py-0.5 rounded-full text-[10px] font-bold bg-slate-500/15 text-slate-400 border border-slate-500/30 transition-all duration-300 cursor-not-allowed';
            badge.textContent = 'OFFLINE';
        } else if (enabled) {
            badge.className = 'px-2 py-0.5 rounded-full text-[10px] font-bold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300 cursor-pointer hover:scale-105';
            badge.textContent = 'HW ON';
        } else {
            badge.className = 'px-2 py-0.5 rounded-full text-[10px] font-bold bg-red-500/15 text-red-400 border border-red-500/30 transition-all duration-300 animate-pulse cursor-pointer hover:scale-105';
            badge.textContent = 'HW OFF';
        }
    }
    if (overlay) {
        if (enabled && !offline) {
            overlay.classList.add('hidden');
            overlay.classList.remove('flex');
        } else {
            overlay.classList.remove('hidden');
            overlay.classList.add('flex');
        }
    }
    if (overlayText) {
        overlayText.textContent = offline ? 'SIMULATOR OFFLINE' : 'HARDWARE DISABLED';
        overlayText.className = offline
            ? 'text-slate-400 font-bold text-sm'
            : 'text-red-400 font-bold text-sm';
    }
    if (overlayBtn) {
        overlayBtn.style.display = offline ? 'none' : '';
    }
}

async function toggleHardware(component) {
    if (!simulatorOnline) {
        Swal.fire({
            icon: 'warning',
            title: 'Simulator Offline',
            text: 'Cannot toggle hardware while simulator is offline.',
            background: '#1e293b',
            color: '#e2e8f0',
            confirmButtonColor: '#2563eb',
            backdrop: 'rgba(0,0,0,0.6)'
        });
        return;
    }
    try {
        const res = await fetch(`/api/vessel/toggle/${component}`, { method: 'POST' });
        if (!res.ok) {
            Swal.fire({
                icon: 'error',
                title: 'Toggle Failed',
                text: 'Could not toggle hardware. Simulator may be offline.',
                background: '#1e293b',
                color: '#e2e8f0',
                confirmButtonColor: '#2563eb',
                backdrop: 'rgba(0,0,0,0.6)'
            });
        }
        // State update will arrive via SignalR ReceiveHardwareState
    } catch {
        Swal.fire({
            icon: 'error',
            title: 'Connection Error',
            text: 'Cannot reach the server.',
            background: '#1e293b',
            color: '#e2e8f0',
            confirmButtonColor: '#2563eb'
        });
    }
}

async function checkSimulatorState() {
    try {
        const res = await fetch('/api/vessel/simulator/state');
        const data = await res.json();
        const badge = document.getElementById('simulatorBadge');
        if (data.isGlobalRunning) {
            badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-green-500/15 text-green-400 border border-green-500/30 transition-all duration-300';
            badge.textContent = '● Running';
        } else {
            badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-yellow-500/15 text-yellow-400 border border-yellow-500/30 transition-all duration-300';
            badge.textContent = '● Standby';
        }

        // Per-hardware status
        simulatorOnline = true;
        setHwBadge('compassHwBadge', 'compassOverlay', data.isCompassEnabled, false);
        setHwBadge('waterHwBadge', 'waterOverlay', data.isWaterEnabled, false);
        setHwBadge('pumpHwBadge', 'pumpOverlay', data.isPumpEnabled, false);
        setHwBadge('ledHwBadge', 'ledOverlay', data.isLedEnabled, false);
    } catch {
        simulatorOnline = false;
        const badge = document.getElementById('simulatorBadge');
        badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-red-500/15 text-red-400 border border-red-500/30 transition-all duration-300 animate-pulse';
        badge.textContent = '● Offline';
        // Mark all hardware as offline
        setHwBadge('compassHwBadge', 'compassOverlay', false, true);
        setHwBadge('waterHwBadge', 'waterOverlay', false, true);
        setHwBadge('pumpHwBadge', 'pumpOverlay', false, true);
        setHwBadge('ledHwBadge', 'ledOverlay', false, true);
    }
}
setInterval(checkSimulatorState, 10000);

// ---- Simulator Heartbeat (5s timeout) ----
setInterval(() => {
    const elapsed = Date.now() - lastSimulatorUpdate;
    if (elapsed > 5000 && simulatorOnline) {
        simulatorOnline = false;
        const badge = document.getElementById('simulatorBadge');
        badge.className = 'px-3 py-1 rounded-full text-xs font-semibold bg-red-500/15 text-red-400 border border-red-500/30 transition-all duration-300 animate-pulse';
        badge.textContent = '● Offline';
        setHwBadge('compassHwBadge', 'compassOverlay', false, true);
        setHwBadge('waterHwBadge', 'waterOverlay', false, true);
        setHwBadge('pumpHwBadge', 'pumpOverlay', false, true);
        setHwBadge('ledHwBadge', 'ledOverlay', false, true);
    }
}, 1000);

// ---- Compass Drawing (Canvas) ----
function drawCompass(heading) {
    const canvas = document.getElementById('compassGauge');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    const w = canvas.width, h = canvas.height;
    const cx = w / 2, cy = h / 2;
    const r = Math.min(cx, cy) - 10;

    ctx.clearRect(0, 0, w, h);

    // Outer glow ring
    const gradient = ctx.createRadialGradient(cx, cy, r - 5, cx, cy, r + 5);
    gradient.addColorStop(0, 'rgba(14,165,233,0.1)');
    gradient.addColorStop(1, 'transparent');
    ctx.beginPath();
    ctx.arc(cx, cy, r + 3, 0, Math.PI * 2);
    ctx.fillStyle = gradient;
    ctx.fill();

    // Outer circle
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.strokeStyle = '#334155';
    ctx.lineWidth = 2;
    ctx.stroke();

    // Inner dial bg
    const dialGrad = ctx.createRadialGradient(cx, cy, 0, cx, cy, r);
    dialGrad.addColorStop(0, '#1e293b');
    dialGrad.addColorStop(1, '#0f172a');
    ctx.beginPath();
    ctx.arc(cx, cy, r - 1, 0, Math.PI * 2);
    ctx.fillStyle = dialGrad;
    ctx.fill();

    // Tick marks
    for (let deg = 0; deg < 360; deg += 5) {
        const angle = (deg - heading - 90) * Math.PI / 180;
        const isMajor = deg % 30 === 0;
        const isMid = deg % 10 === 0;
        const tickLen = isMajor ? 14 : (isMid ? 8 : 4);
        const x1 = cx + (r - 3) * Math.cos(angle);
        const y1 = cy + (r - 3) * Math.sin(angle);
        const x2 = cx + (r - 3 - tickLen) * Math.cos(angle);
        const y2 = cy + (r - 3 - tickLen) * Math.sin(angle);

        ctx.beginPath();
        ctx.moveTo(x1, y1);
        ctx.lineTo(x2, y2);
        ctx.strokeStyle = isMajor ? '#64748b' : (isMid ? '#475569' : '#334155');
        ctx.lineWidth = isMajor ? 2 : 1;
        ctx.stroke();
    }

    // Cardinal marks
    const cardinals = [
        { label: 'N', deg: 0, color: '#ef4444' },
        { label: 'NE', deg: 45, color: '#64748b' },
        { label: 'E', deg: 90, color: '#94a3b8' },
        { label: 'SE', deg: 135, color: '#64748b' },
        { label: 'S', deg: 180, color: '#94a3b8' },
        { label: 'SW', deg: 225, color: '#64748b' },
        { label: 'W', deg: 270, color: '#94a3b8' },
        { label: 'NW', deg: 315, color: '#64748b' }
    ];
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';

    for (const c of cardinals) {
        const angle = (c.deg - heading - 90) * Math.PI / 180;
        const dist = c.label.length === 1 ? r - 25 : r - 24;
        const tx = cx + dist * Math.cos(angle);
        const ty = cy + dist * Math.sin(angle);
        ctx.font = c.label.length === 1 ? 'bold 14px sans-serif' : '10px sans-serif';
        ctx.fillStyle = c.color;
        ctx.fillText(c.label, tx, ty);
    }

    // Degree numbers for major ticks
    ctx.font = '8px monospace';
    ctx.fillStyle = '#475569';
    for (let deg = 0; deg < 360; deg += 30) {
        if (deg % 90 === 0) continue; // skip cardinals
        const angle = (deg - heading - 90) * Math.PI / 180;
        const tx = cx + (r - 38) * Math.cos(angle);
        const ty = cy + (r - 38) * Math.sin(angle);
        ctx.fillText(`${deg}`, tx, ty);
    }

    // North needle (red, pointed)
    ctx.save();
    ctx.translate(cx, cy);
    ctx.beginPath();
    ctx.moveTo(0, -(r - 40));
    ctx.lineTo(-5, 0);
    ctx.lineTo(0, -8);
    ctx.lineTo(5, 0);
    ctx.closePath();
    ctx.fillStyle = '#ef4444';
    ctx.shadowColor = '#ef4444';
    ctx.shadowBlur = 8;
    ctx.fill();
    ctx.shadowBlur = 0;

    // South needle
    ctx.beginPath();
    ctx.moveTo(0, r - 40);
    ctx.lineTo(-5, 0);
    ctx.lineTo(0, 8);
    ctx.lineTo(5, 0);
    ctx.closePath();
    ctx.fillStyle = '#334155';
    ctx.fill();
    ctx.restore();

    // Center hub
    ctx.beginPath();
    ctx.arc(cx, cy, 6, 0, Math.PI * 2);
    ctx.fillStyle = '#e2e8f0';
    ctx.fill();
    ctx.beginPath();
    ctx.arc(cx, cy, 3, 0, Math.PI * 2);
    ctx.fillStyle = '#94a3b8';
    ctx.fill();
}

// ---- Pump Control ----
function updatePumpVisual(isOn) {
    const icon = document.getElementById('pumpIcon');
    const status = document.getElementById('pumpStatus');
    const badge = document.getElementById('pumpBadge');

    if (isOn) {
        icon.classList.add('pump-spin');
        icon.className = icon.className.replace('bg-slate-700', 'bg-emerald-900/50');
        status.textContent = 'Running';
        status.className = 'text-emerald-400 text-sm font-semibold';
        badge.className = 'px-2.5 py-0.5 rounded-full text-xs font-bold bg-emerald-500/20 text-emerald-400 transition-all';
        badge.textContent = 'ON';
    } else {
        icon.classList.remove('pump-spin');
        icon.className = 'w-16 h-16 rounded-full bg-slate-700 flex items-center justify-center text-3xl transition-all duration-300';
        status.textContent = 'Idle';
        status.className = 'text-slate-500 text-sm font-medium';
        badge.className = 'px-2.5 py-0.5 rounded-full text-xs font-bold bg-slate-600/50 text-slate-400 transition-all';
        badge.textContent = 'OFF';
    }
}

async function togglePump() {
    const toggle = document.getElementById('pumpToggle');
    const wantOn = toggle.checked;

    try {
        const res = await fetch('/api/vessel/pump', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ isOn: wantOn, message: wantOn ? 'Manual ON' : 'Manual OFF' })
        });

        if (res.ok) {
            updatePumpVisual(wantOn);
        } else {
            toggle.checked = !wantOn;
            Swal.fire({
                icon: 'error',
                title: 'Pump Control Failed',
                text: 'Hardware disabled or simulator offline.',
                background: '#1e293b',
                color: '#e2e8f0',
                confirmButtonColor: '#2563eb',
                backdrop: 'rgba(0,0,0,0.6)'
            });
        }
    } catch {
        toggle.checked = !wantOn;
        Swal.fire({
            icon: 'error',
            title: 'Connection Error',
            text: 'Cannot reach the server.',
            background: '#1e293b',
            color: '#e2e8f0',
            confirmButtonColor: '#2563eb'
        });
    }
}

// ---- LED Control ----
function toggleLed() {
    const toggle = document.getElementById('ledToggle');
    ledIsOn = toggle.checked;

    const controls = document.getElementById('ledControls');
    const badge = document.getElementById('ledBadge');
    const dot = document.getElementById('ledDot');

    if (ledIsOn) {
        controls.classList.remove('opacity-30', 'pointer-events-none');
        controls.classList.add('opacity-100');
        badge.className = 'px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-500/20 text-amber-400 transition-all';
        badge.textContent = 'ON';
        // Send current color
        const color = document.getElementById('ledColorPicker').value;
        setLedColor(color);
    } else {
        controls.classList.add('opacity-30', 'pointer-events-none');
        controls.classList.remove('opacity-100');
        badge.className = 'px-2.5 py-0.5 rounded-full text-xs font-bold bg-slate-600/50 text-slate-400 transition-all';
        badge.textContent = 'OFF';
        dot.className = 'w-2 h-2 rounded-full bg-slate-500 inline-block transition-all duration-300';
        // Turn off: send black / 0 brightness
        setLedColor('#000000', true);
    }
}

async function setLedColor(hexColor, forceOff) {
    const brightness = forceOff ? 0 : parseInt(document.getElementById('ledBrightness').value);
    if (!forceOff) {
        document.getElementById('brightnessVal').textContent = `${brightness}%`;
    }

    try {
        const res = await fetch('/api/vessel/led', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ hexColor: hexColor, brightness: brightness })
        });

        if (res.ok) {
            if (forceOff) {
                updateLedVisual('#000000', 0);
            } else {
                updateLedVisual(hexColor, brightness);
            }
        } else {
            if (ledIsOn) {
                document.getElementById('ledToggle').checked = false;
                ledIsOn = false;
                toggleLed();
            }
            Swal.fire({
                icon: 'error',
                title: 'LED Control Failed',
                text: 'Hardware disabled or simulator offline.',
                background: '#1e293b',
                color: '#e2e8f0',
                confirmButtonColor: '#2563eb'
            });
        }
    } catch {
        Swal.fire({
            icon: 'error',
            title: 'Connection Error',
            text: 'Cannot reach the server.',
            background: '#1e293b',
            color: '#e2e8f0',
            confirmButtonColor: '#2563eb'
        });
    }
}

function updateLedVisual(hexColor, brightness) {
    const orb = document.getElementById('ledOrb');
    const hexText = document.getElementById('ledHexText');
    const dot = document.getElementById('ledDot');
    const opacity = brightness / 100;
    const isOff = hexColor === '#000000' || brightness === 0;

    if (isOff) {
        orb.style.background = '#334155';
        orb.style.borderColor = '#475569';
        orb.style.boxShadow = 'none';
        dot.className = 'w-2 h-2 rounded-full bg-slate-500 inline-block transition-all duration-300';
        hexText.textContent = '#000000';
    } else {
        orb.style.background = `radial-gradient(circle at 40% 40%, ${hexColor}dd, ${hexColor}66)`;
        orb.style.borderColor = hexColor;
        orb.style.boxShadow = `0 0 ${20 * opacity}px ${hexColor}, 0 0 ${50 * opacity}px ${hexColor}44, inset 0 0 ${15 * opacity}px ${hexColor}66`;
        dot.style.backgroundColor = hexColor;
        dot.style.boxShadow = `0 0 6px ${hexColor}`;
        dot.className = 'w-2 h-2 rounded-full inline-block transition-all duration-300';
        hexText.textContent = hexColor.toUpperCase();
    }
}

// ---- Modals ----
function openModal(id) {
    const modal = document.getElementById(id);
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    // Animate in
    const panel = modal.querySelector('.relative');
    if (panel) {
        panel.style.transform = 'scale(0.95)';
        panel.style.opacity = '0';
        requestAnimationFrame(() => {
            panel.style.transition = 'transform 0.2s ease, opacity 0.2s ease';
            panel.style.transform = 'scale(1)';
            panel.style.opacity = '1';
        });
    }
}

function closeModal(id) {
    const modal = document.getElementById(id);
    const panel = modal.querySelector('.relative');
    if (panel) {
        panel.style.transform = 'scale(0.95)';
        panel.style.opacity = '0';
        setTimeout(() => {
            modal.classList.add('hidden');
            modal.classList.remove('flex');
        }, 200);
    } else {
        modal.classList.add('hidden');
        modal.classList.remove('flex');
    }
}

// ---- Water History Modal ----
async function openWaterHistory() {
    openModal('waterHistoryModal');

    try {
        const res = await fetch('/api/vessel/history/water');
        const data = await res.json();

        const labels = data.map(d => new Date(d.timestamp).toLocaleTimeString());
        const levels = data.map(d => d.level);

        if (waterChart) waterChart.destroy();

        const ctx = document.getElementById('waterChart').getContext('2d');
        const gradientFill = ctx.createLinearGradient(0, 0, 0, 300);
        gradientFill.addColorStop(0, 'rgba(59,130,246,0.3)');
        gradientFill.addColorStop(1, 'rgba(59,130,246,0.02)');

        waterChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Water Level (%)',
                    data: levels,
                    borderColor: '#3b82f6',
                    backgroundColor: gradientFill,
                    borderWidth: 2,
                    fill: true,
                    tension: 0.4,
                    pointRadius: 0,
                    pointHoverRadius: 5,
                    pointHoverBackgroundColor: '#3b82f6',
                    pointHoverBorderColor: '#fff',
                    pointHoverBorderWidth: 2
                }]
            },
            options: {
                responsive: true,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { labels: { color: '#94a3b8', usePointStyle: true, padding: 20 } },
                    tooltip: {
                        backgroundColor: '#1e293b',
                        titleColor: '#94a3b8',
                        bodyColor: '#e2e8f0',
                        borderColor: '#334155',
                        borderWidth: 1,
                        cornerRadius: 8,
                        padding: 12
                    }
                },
                scales: {
                    x: {
                        ticks: { color: '#475569', maxTicksLimit: 8, font: { size: 11 } },
                        grid: { color: '#1e293b44' }
                    },
                    y: {
                        min: 0, max: 100,
                        ticks: { color: '#475569', font: { size: 11 } },
                        grid: { color: '#1e293b44' }
                    }
                }
            }
        });
    } catch {
        console.error('Failed to load water history');
    }
}

// ---- Pump Log Modal ----
async function openPumpLogs() {
    openModal('pumpLogModal');

    try {
        const res = await fetch('/api/vessel/history/pump');
        const data = await res.json();

        const tbody = document.getElementById('pumpLogBody');
        if (data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="3" class="py-8 text-center text-slate-500">No pump activity recorded yet.</td></tr>';
            return;
        }

        tbody.innerHTML = data.map((log, i) => {
            const date = new Date(log.timestamp);
            const stateClass = log.isOn ? 'text-emerald-400' : 'text-rose-400';
            const stateIcon = log.isOn ? '▲' : '▼';
            const stateText = log.isOn ? 'ON' : 'OFF';
            const rowBg = i % 2 === 0 ? 'bg-slate-800/30' : '';
            return `
                <tr class="border-b border-slate-700/30 ${rowBg} hover:bg-slate-700/20 transition-colors">
                    <td class="py-2.5 pr-4 text-slate-400 font-mono text-xs">${date.toLocaleString()}</td>
                    <td class="py-2.5 pr-4"><span class="${stateClass} font-bold">${stateIcon} ${stateText}</span></td>
                    <td class="py-2.5 text-slate-300">${log.message}</td>
                </tr>`;
        }).join('');
    } catch {
        console.error('Failed to load pump logs');
    }
}

// ---- Compass Log Modal ----
async function openCompassLogs() {
    openModal('compassLogModal');

    try {
        const res = await fetch('/api/vessel/history/compass');
        const data = await res.json();

        const tbody = document.getElementById('compassLogBody');
        if (data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="3" class="py-8 text-center text-slate-500">No compass data recorded yet.</td></tr>';
            return;
        }

        tbody.innerHTML = data.map((log, i) => {
            const date = new Date(log.timestamp);
            const rowBg = i % 2 === 0 ? 'bg-slate-800/30' : '';
            return `
                <tr class="border-b border-slate-700/30 ${rowBg} hover:bg-slate-700/20 transition-colors">
                    <td class="py-2.5 pr-4 text-slate-400 font-mono text-xs">${date.toLocaleString()}</td>
                    <td class="py-2.5 pr-4"><span class="text-cyan-400 font-bold font-mono">${log.heading}°</span></td>
                    <td class="py-2.5"><span class="px-2 py-0.5 rounded bg-cyan-500/15 text-cyan-400 text-xs font-bold">${log.cardinal}</span></td>
                </tr>`;
        }).join('');
    } catch {
        console.error('Failed to load compass logs');
    }
}

// ---- LED Log Modal ----
async function openLedLogs() {
    openModal('ledLogModal');

    try {
        const res = await fetch('/api/vessel/history/led');
        const data = await res.json();

        const tbody = document.getElementById('ledLogBody');
        if (data.length === 0) {
            tbody.innerHTML = '<tr><td colspan="2" class="py-8 text-center text-slate-500">No LED data recorded yet.</td></tr>';
            return;
        }

        tbody.innerHTML = data.map((log, i) => {
            const date = new Date(log.timestamp);
            const rowBg = i % 2 === 0 ? 'bg-slate-800/30' : '';
            const color = log.hexColor || '#000000';
            return `
                <tr class="border-b border-slate-700/30 ${rowBg} hover:bg-slate-700/20 transition-colors">
                    <td class="py-2.5 pr-4 text-slate-400 font-mono text-xs">${date.toLocaleString()}</td>
                    <td class="py-2.5">
                        <div class="flex items-center gap-2">
                            <span class="w-5 h-5 rounded-full border border-slate-600 inline-block" style="background:${color}; box-shadow: 0 0 8px ${color}55;"></span>
                            <span class="font-mono text-amber-400 font-bold">${color.toUpperCase()}</span>
                        </div>
                    </td>
                </tr>`;
        }).join('');
    } catch {
        console.error('Failed to load LED logs');
    }
}

// ---- Init ----
document.addEventListener('DOMContentLoaded', () => {
    drawCompass(0);
    startConnection();
});
```

---

## Langkah 9: Generate EF Core Migration

```powershell
cd MVCS.Server

# Install EF Core CLI tool (jika belum)
dotnet tool install --global dotnet-ef

# Generate initial migration
dotnet ef migrations add InitialCreate

cd ..
```

> Migration akan otomatis berjalan saat aplikasi start (`db.Database.Migrate()` di `Program.cs`), jadi tidak perlu `dotnet ef database update` manual.

---

## Langkah 10: Build & Run

```powershell
# Build dari root folder
dotnet build MVCS.sln
# Pastikan: Build succeeded. 0 Warning(s) 0 Error(s)
```

### Jalankan (2 terminal terpisah):

**Terminal 1 — Server:**
```powershell
cd MVCS.Server
dotnet run
```

**Terminal 2 — Simulator:**
```powershell
cd MVCS.Simulator
dotnet run
```

### Akses:

| URL | Keterangan |
|-----|-----------|
| http://localhost:5000 | Server landing page |
| http://localhost:5000/Account/Login | Login (admin@mvcs.com / Admin123) |
| http://localhost:5000/Dashboard | Server real-time dashboard |
| http://localhost:5100 | Simulator landing page |
| http://localhost:5100/Dashboard | Simulator dashboard (hardware control) |
| http://localhost:5100/swagger | Simulator Swagger UI |

---

## Ringkasan Struktur File

```
Sensor Control/
├── MVCS.sln
├── MVCS.Shared/
│   ├── MVCS.Shared.csproj
│   └── DTOs/
│       ├── CompassDto.cs
│       ├── WaterLevelDto.cs
│       ├── PumpStateDto.cs
│       ├── LedStateDto.cs
│       └── SimulationStateDto.cs
├── MVCS.Simulator/
│   ├── MVCS.Simulator.csproj
│   ├── Program.cs
│   ├── Services/
│   │   ├── SimulationStateService.cs
│   │   └── SimulatorHubClient.cs
│   ├── Hubs/
│   │   ├── SimulatorHub.cs
│   │   └── SimulatorDashboardHub.cs    ← BARU
│   ├── Workers/
│   │   ├── CompassBroadcaster.cs
│   │   └── WaterBroadcaster.cs
│   ├── Controllers/
│   │   ├── HardwareController.cs
│   │   ├── SimulationController.cs
│   │   ├── HomeViewController.cs       ← BARU
│   │   └── DashboardViewController.cs  ← BARU
│   ├── Views/                          ← BARU
│   │   ├── _ViewImports.cshtml
│   │   ├── _ViewStart.cshtml
│   │   ├── Shared/_Layout.cshtml
│   │   ├── HomeView/Index.cshtml
│   │   └── DashboardView/Index.cshtml
│   └── wwwroot/                        ← BARU
│       └── js/simulator-dashboard.js
└── MVCS.Server/
    ├── MVCS.Server.csproj
    ├── Program.cs
    ├── Data/
    │   └── ApplicationDbContext.cs
    ├── Models/
    │   ├── LogModels.cs
    │   ├── AccountViewModels.cs
    │   └── ErrorViewModel.cs
    ├── Services/
    │   ├── LogService.cs
    │   ├── SimulatorConnectionService.cs
    │   └── ServerHubClient.cs
    ├── Hubs/
    │   └── VesselHub.cs
    ├── Controllers/
    │   ├── HomeController.cs
    │   ├── AccountController.cs
    │   ├── DashboardController.cs
    │   └── VesselApiController.cs
    ├── Views/
    │   ├── _ViewImports.cshtml
    │   ├── _ViewStart.cshtml
    │   ├── Shared/_Layout.cshtml
    │   ├── Home/Index.cshtml
    │   ├── Account/Login.cshtml
    │   ├── Account/Register.cshtml
    │   └── Dashboard/Index.cshtml
    ├── Migrations/            ← auto-generated
    └── wwwroot/
        ├── css/site.css
        └── js/dashboard.js
```

**Total: 40 file source code** (tidak termasuk migration files yang auto-generated).
