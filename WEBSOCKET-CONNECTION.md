# 🔌 WebSocket Connection Guide — MVCS SignalR Architecture

Dokumentasi lengkap cara kerja koneksi WebSocket (SignalR) antara **MVCS.Simulator** dan **MVCS.Server**.

> **Updated:** Dokumen ini telah diperbarui untuk mencerminkan implementasi best practices — interface-based DI, config-driven URLs, thread safety, input validation, buffered logging, dan improved error handling.

---

## Daftar Isi

1. [Overview Arsitektur](#1-overview-arsitektur)
2. [Konsep Dasar SignalR](#2-konsep-dasar-signalr)
3. [Dual-Direction Connection Pattern](#3-dual-direction-connection-pattern)
4. [Step-by-Step: Setup SignalR di Program.cs](#4-step-by-step-setup-signalr-di-programcs)
5. [Koneksi 1: Simulator → Server (Data Push)](#5-koneksi-1-simulator--server-data-push)
6. [Koneksi 2: Server → Simulator (Command)](#6-koneksi-2-server--simulator-command)
7. [Koneksi 3: Browser Dashboard (Frontend)](#7-koneksi-3-browser-dashboard-frontend)
8. [Data Transfer Objects (DTOs)](#8-data-transfer-objects-dtos)
9. [Background Workers (Sensor Broadcasters)](#9-background-workers-sensor-broadcasters)
10. [Auto-Reconnect & Fault Tolerance](#10-auto-reconnect--fault-tolerance)
11. [Best Practices yang Diterapkan](#11-best-practices-yang-diterapkan)
12. [Sequence Diagrams](#12-sequence-diagrams)
13. [File Reference Map](#13-file-reference-map)

---

## 1. Overview Arsitektur

Sistem MVCS menggunakan **3 jenis koneksi SignalR** yang berjalan bersamaan:

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                        ARSITEKTUR KONEKSI SIGNALR                           │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   Browser (:5000)           Browser (:5100)                                  │
│   ┌──────────────┐         ┌──────────────────┐                              │
│   │ dashboard.js │         │ sim-dashboard.js  │                              │
│   │ SignalR JS   │         │ SignalR JS        │                              │
│   └──────┬───────┘         └────────┬──────────┘                              │
│          │ Koneksi 3a               │ Koneksi 3b                              │
│          │ ws://:5000/vesselhub     │ ws://:5100/simulatordashboardhub        │
│          ▼                          ▼                                         │
│   ┌─────────────────────┐   ┌──────────────────────────┐                      │
│   │  MVCS.Server :5000  │   │   MVCS.Simulator :5100   │                      │
│   │                     │   │                          │                      │
│   │  VesselHub ◄────────┼───┤ ISimulatorHubClient      │  Koneksi 1           │
│   │  (receives data)    │   │ (pushes sensor data)     │  DATA PUSH           │
│   │  + input validation │   │ + SendAsync (fire&forget)│                      │
│   │                     │   │                          │                      │
│   │  IServerHubClient ──┼──►│ SimulatorHub             │  Koneksi 2           │
│   │  (sends commands)   │   │ (receives commands)      │  COMMAND             │
│   │  + 10s timeout      │   │                          │  + timeout           │
│   └─────────────────────┘   └──────────────────────────┘                      │
│                                                                              │
│   Koneksi 1: Simulator pushes data sensor → Server (fire-and-forget)         │
│   Koneksi 2: Server sends commands → Simulator (request-response + timeout)  │
│   Koneksi 3: Browser ↔ Hub lokal (real-time UI update)                       │
└──────────────────────────────────────────────────────────────────────────────┘
```

**Kenapa 2 koneksi terpisah (bukan 1)?**
- **Separation of Concerns**: Data push dan command channel punya kebutuhan berbeda
- **Independence**: Server bisa mati tanpa mengganggu simulator, dan sebaliknya
- **Scalability**: Masing-masing bisa di-scale secara independen

---

## 2. Konsep Dasar SignalR

SignalR adalah library .NET untuk komunikasi real-time yang menggunakan **WebSocket** sebagai transport utama.

### Terminologi Penting

| Istilah | Penjelasan | Contoh di MVCS |
|---------|-----------|----------------|
| **Hub** | Server-side class yang meng-expose method untuk dipanggil client | `VesselHub`, `SimulatorHub` |
| **HubConnection** | Client-side object yang connect ke Hub | `SimulatorHubClient`, `ServerHubClient` |
| **InvokeAsync** | Client memanggil method di Hub, **menunggu return value** (+ timeout) | `SendPumpCommandAsync()` |
| **SendAsync** | Fire-and-forget, **tidak menunggu response** | `PushCompassAsync()` |
| **Group** | Logical grouping dari connections | `"Dashboard"`, `"Simulator"` |
| **HostedService** | Background service yang auto-start saat aplikasi run | `SimulatorHubClient : IHostedService` |
| **Interface** | Abstraksi untuk dependency injection & testability | `IServerHubClient`, `ISimulatorHubClient` |

### Hub vs HubConnection

```
Hub (Server-side)                    HubConnection (Client-side)
─────────────────                    ─────────────────────────────
- Meng-host endpoint                 - Connect ke endpoint
- Menerima panggilan dari client     - Memanggil method di Hub
- Bisa broadcast ke semua client     - Menerima pesan dari Hub
- Tahu siapa yang connected          - Punya state: Connected/Reconnecting/Disconnected
- Validasi input data yang masuk     - URL dari config (bukan hardcoded)
```

---

## 3. Dual-Direction Connection Pattern

### Kenapa tiap app punya Hub DAN HubConnection?

```
MVCS.Server                              MVCS.Simulator
════════════                              ════════════════
Punya Hub:        VesselHub               Punya Hub:        SimulatorHub
                  (menerima data)                           (menerima command)
                  + input validation                        + logging

Punya Client:     IServerHubClient        Punya Client:     ISimulatorHubClient
                  (mengirim command)                         (mengirim data)
                  + 10s timeout                              + SendAsync (fire&forget)
```

Setiap aplikasi bertindak sebagai **listener** (Hub) sekaligus **broadcaster** (HubConnection) — ini yang membuat komunikasi **bidirectional**.

Semua dependency di-inject via **interface** (bukan concrete class) sesuai SOLID principles.

---

## 4. Step-by-Step: Setup SignalR di Program.cs

### Step 4.1: Server — `MVCS.Server/Program.cs`

```csharp
// ❶ Tambahkan SignalR server service
builder.Services.AddSignalR();

// ❷ Register services via INTERFACE (Dependency Inversion Principle)
builder.Services.AddSingleton<ISimulatorConnectionService, SimulatorConnectionService>();

// ❸ Register LogService sebagai buffered writer (IHostedService + ILogService)
builder.Services.AddSingleton<LogService>();
builder.Services.AddSingleton<ILogService>(sp => sp.GetRequiredService<LogService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<LogService>());

// ❹ Register outbound SignalR client (IServerHubClient + IHostedService)
builder.Services.AddSingleton<ServerHubClient>();
builder.Services.AddSingleton<IServerHubClient>(sp => sp.GetRequiredService<ServerHubClient>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ServerHubClient>());

// ❺ Health checks
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");

// ❻ Map hub endpoint + health check (setelah app.Build())
app.MapHub<VesselHub>("/vesselhub");
app.MapHealthChecks("/health");
```

**Penjelasan registration pattern:**
- `AddSingleton<ServerHubClient>()` → Buat 1 instance yang di-share ke seluruh app
- `AddSingleton<IServerHubClient>(...)` → Expose via interface untuk consumer DI
- `AddHostedService(...)` → Otomatis panggil `StartAsync()` saat app start
- Mengambil instance yang sama (`GetRequiredService`) bukan membuat baru

### Step 4.2: Simulator — `MVCS.Simulator/Program.cs`

```csharp
// ❶ Tambahkan SignalR server service
builder.Services.AddSignalR();

// ❷ Register state service via INTERFACE (thread-safe singleton)
builder.Services.AddSingleton<SimulationStateService>();
builder.Services.AddSingleton<ISimulationStateService>(sp => sp.GetRequiredService<SimulationStateService>());

// ❸ Register outbound client via INTERFACE (ISimulatorHubClient + IHostedService)
builder.Services.AddSingleton<SimulatorHubClient>();
builder.Services.AddSingleton<ISimulatorHubClient>(sp => sp.GetRequiredService<SimulatorHubClient>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<SimulatorHubClient>());

// ❹ Register background workers (sensor simulators)
builder.Services.AddHostedService<CompassBroadcaster>();
builder.Services.AddHostedService<WaterBroadcaster>();

// ❺ Map hub endpoints (setelah app.Build())
app.MapHub<SimulatorHub>("/simulatorhub");                     // Server connects here
app.MapHub<SimulatorDashboardHub>("/simulatordashboardhub");   // Browser connects here
```

---

## 5. Koneksi 1: Simulator → Server (Data Push)

**Tujuan:** Simulator mengirimkan data sensor (compass, water level, pump state, LED state) ke Server secara real-time.

### Step 5.1: Simulator Membangun Koneksi

File: `MVCS.Simulator/Services/SimulatorHubClient.cs` — implements `IHostedService, ISimulatorHubClient`

```csharp
public class SimulatorHubClient : IHostedService, ISimulatorHubClient
{
    private HubConnection? _hub;
    private readonly ISimulationStateService _state;  // ← Interface, bukan concrete class
    private readonly string _serverHubUrl;             // ← Dari config, bukan hardcoded

    public SimulatorHubClient(ISimulationStateService state, ILogger<SimulatorHubClient> logger,
        IConfiguration configuration)
    {
        _state = state;
        _serverHubUrl = configuration["SignalR:ServerHubUrl"]
            ?? throw new InvalidOperationException("SignalR:ServerHubUrl is not configured");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // ❶ Build koneksi ke Server's VesselHub (URL dari appsettings.json)
        _hub = new HubConnectionBuilder()
            .WithUrl($"{_serverHubUrl}?role=simulator")
            //          ▲ dari config              ▲ query string identifier
            .WithAutomaticReconnect(new[] {
                TimeSpan.Zero,            // Retry langsung
                TimeSpan.FromSeconds(2),  // 2 detik kemudian
                TimeSpan.FromSeconds(5),  // 5 detik kemudian
                TimeSpan.FromSeconds(10), // 10 detik kemudian
                TimeSpan.FromSeconds(30)  // Max: setiap 30 detik
            })
            .Build();

        // ❷ Setup event handlers
        _hub.Reconnecting += ex => { /* log warning */ };
        _hub.Reconnected += connectionId => {
            // Setelah reconnect, sync ulang state
            _ = PushHardwareStateAsync();
        };
        _hub.Closed += ex => { /* log warning */ };

        // ❸ Fire-and-forget connect (non-blocking!)
        _ = ConnectWithRetryAsync(cancellationToken);
        return Task.CompletedTask;
    }
}
```

**Key insight:** `?role=simulator` di URL digunakan oleh `VesselHub.OnConnectedAsync()` untuk membedakan koneksi Simulator vs Browser.

### Step 5.2: Connect dengan Retry Loop

```csharp
private async Task ConnectWithRetryAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        try
        {
            await _hub!.StartAsync(ct);          // Coba connect
            _logger.LogInformation("Connected to Server SignalR hub at {Url}", _serverHubUrl);
            await PushHardwareStateAsync();       // Berhasil → kirim state awal
            return;                               // Keluar loop
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to connect: {Message}. Retrying in 3s...", ex.Message);
            try { await Task.Delay(3000, ct); }
            catch (OperationCanceledException) { return; }
        }
    }
}
```

**Pattern penting:** Simulator **tidak crash** jika Server belum siap. Ia terus retry setiap 3 detik.

### Step 5.3: Server Menerima Koneksi

File: `MVCS.Server/Hubs/VesselHub.cs`

```csharp
public class VesselHub : Hub
{
    private readonly ISimulatorConnectionService _simConn;  // ← Interface
    private readonly ILogService _logService;                // ← Interface (buffered writer)
    private readonly ILogger<VesselHub> _logger;

    public override async Task OnConnectedAsync()
    {
        // ❶ Cek query string untuk identifikasi role
        var role = Context.GetHttpContext()?.Request.Query["role"].ToString();

        if (role == "simulator")
        {
            // ❷ Simpan connection ID (thread-safe via lock)
            _simConn.SimulatorConnectionId = Context.ConnectionId;

            // ❸ Masukkan ke SignalR Group "Simulator"
            await Groups.AddToGroupAsync(Context.ConnectionId, "Simulator");

            // ❹ Jika ada cached state, kirim ke dashboard browser
            if (_simConn.LastKnownState != null)
                await Clients.Group("Dashboard").SendAsync("ReceiveHardwareState",
                    _simConn.LastKnownState);
        }
        else
        {
            // Browser dashboard → masukkan ke group "Dashboard"
            await Groups.AddToGroupAsync(Context.ConnectionId, "Dashboard");
            await Clients.Caller.SendAsync("ConnectionStatus", true);
        }
    }
}
```

### Step 5.4: Simulator Mengirim Data via Push Methods

```csharp
// Di SimulatorHubClient — menggunakan SendAsync (fire-and-forget, lebih efisien)
public async Task PushCompassAsync(int heading, string cardinal)
{
    if (!IsConnected)
    {
        _logger.LogDebug("Skipping compass push — not connected to Server");
        return;
    }

    try
    {
        // ✅ SendAsync = fire-and-forget (bukan InvokeAsync yang menunggu response)
        await _hub!.SendAsync("SimPushCompass", heading, cardinal);
    }
    catch (Exception ex)
    {
        // ✅ LogError (bukan LogWarning) untuk actual failures
        _logger.LogError(ex, "Failed to push compass data to Server hub");
    }
}
```

> **Best Practice:** Push methods menggunakan `SendAsync` (fire-and-forget) karena tidak butuh response. Lebih efisien daripada `InvokeAsync` yang menunggu acknowledgment.

### Step 5.5: Server Menerima & Meneruskan ke Browser (+ Input Validation)

```csharp
// Di VesselHub — dipanggil oleh SimulatorHubClient
public async Task SimPushCompass(int heading, string cardinal)
{
    // ✅ Input validation — heading harus 0-359
    if (heading < 0 || heading >= 360)
    {
        _logger.LogWarning("Invalid compass heading received: {Heading}", heading);
        return;
    }
    if (string.IsNullOrWhiteSpace(cardinal))
    {
        _logger.LogWarning("Empty cardinal direction received");
        return;
    }

    // ❶ Buffer ke Channel<T> (flush ke SQLite setiap 5 detik, bukan per-record)
    await _logService.LogCompassAsync(heading, cardinal);

    // ❷ Forward ke browser dashboard
    await Clients.Group("Dashboard").SendAsync("ReceiveCompass", heading, cardinal);
}
```

---

## 6. Koneksi 2: Server → Simulator (Command)

**Tujuan:** Server mengirimkan command kontrol (pump on/off, LED color, toggle hardware) ke Simulator.

### Step 6.1: Server Membangun Koneksi

File: `MVCS.Server/Services/ServerHubClient.cs` — implements `IHostedService, IServerHubClient`

```csharp
public class ServerHubClient : IHostedService, IServerHubClient
{
    private readonly string _simulatorHubUrl;
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);

    public ServerHubClient(ILogger<ServerHubClient> logger, IConfiguration configuration)
    {
        _logger = logger;
        // ✅ URL dari appsettings.json — bukan hardcoded
        _simulatorHubUrl = configuration["SignalR:SimulatorHubUrl"]
            ?? throw new InvalidOperationException("SignalR:SimulatorHubUrl is not configured");
    }

    // Build connection (URL dari config)
    _hub = new HubConnectionBuilder()
        .WithUrl(_simulatorHubUrl)                    // ✅ Dari config
        .WithAutomaticReconnect(/* backoff policy */)
        .Build();

    _ = ConnectWithRetryAsync(cancellationToken);     // Non-blocking retry
}
```

### Step 6.2: Server Mengirim Command (Request-Response + Timeout)

```csharp
// InvokeAsync<T> = panggil method DAN tunggu return value (dengan timeout!)
public async Task<string> SendPumpCommandAsync(bool isOn, string message)
{
    if (!IsConnected)
        throw new InvalidOperationException("Not connected to Simulator");

    // ✅ 10 detik timeout — tidak bisa hang selamanya
    using var cts = new CancellationTokenSource(CommandTimeout);
    var result = await _hub!.InvokeAsync<object>("ExecutePumpCommand", isOn, message, cts.Token);
    return JsonSerializer.Serialize(result);
}
```

**InvokeAsync vs SendAsync:**
| Method | Blocking? | Return Value? | Timeout? | Use Case |
|--------|-----------|---------------|----------|----------|
| `InvokeAsync<T>` | Ya (await) | Ya | ✅ via CancellationToken | Command yang butuh response |
| `SendAsync` | Tidak | Tidak | N/A | Fire-and-forget push data |

### Step 6.3: Simulator Menerima & Mengeksekusi Command

File: `MVCS.Simulator/Hubs/SimulatorHub.cs`

```csharp
public class SimulatorHub : Hub
{
    private readonly ISimulationStateService _state;      // ← Interface (thread-safe)
    private readonly ISimulatorHubClient _hubClient;      // ← Interface
    private readonly IHubContext<SimulatorDashboardHub> _dashboardHub;
    private readonly ILogger<SimulatorHub> _logger;

    public async Task<object> ExecutePumpCommand(bool isOn, string message)
    {
        // ❶ Guard: cek apakah hardware enabled
        if (!_state.State.IsPumpEnabled)
            return new { error = "Pump is disabled", disabled = true };

        // ❷ Update state lokal (thread-safe via internal locking)
        _state.PumpIsOn = isOn;
        var result = new PumpStateDto
        {
            IsOn = _state.PumpIsOn,
            Message = _state.PumpIsOn ? "Pump activated" : "Pump deactivated"
        };

        // ❸ Push update BALIK ke Server via Koneksi 1
        await _hubClient.PushPumpStateAsync(result.IsOn, result.Message);

        // ❹ Push ke local dashboard (Koneksi 3b)
        await _dashboardHub.Clients.All.SendAsync("ReceivePumpState", result.IsOn, result.Message);

        _logger.LogInformation("Pump command executed: IsOn={IsOn}", result.IsOn);

        // ❺ Return langsung ke Server sebagai response
        return result;
    }
}
```

---

## 7. Koneksi 3: Browser Dashboard (Frontend)

### Step 7.1: Server Dashboard — `dashboard.js`

```javascript
// ❶ Build koneksi ke VesselHub lokal
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/vesselhub")                            // Relative URL (same origin)
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();

// ❷ Register event handlers
connection.on("ReceiveCompass", (heading, cardinal) => {
    drawCompass(heading);  // Update canvas compass
});

connection.on("ReceiveWaterLevel", (level, status) => {
    // Update water tank visual
});

connection.on("ReceiveHardwareState", (state) => {
    // Update hardware badges (HW ON/OFF/OFFLINE)
});

// ❸ Start connection
await connection.start();
```

### Step 7.2: Bagaimana Browser Mengirim Command

Browser **TIDAK** berkomunikasi langsung via SignalR untuk command. Sebaliknya, melalui **REST API** (yang sekarang dilindungi `[Authorize]`):

```javascript
// Browser → REST API (perlu autentikasi) → ServerHubClient → SimulatorHub
const res = await fetch('/api/vessel/pump', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ isOn: true, message: 'Manual ON' })
});
```

**Alur lengkap:**
```
Browser → POST /api/vessel/pump → VesselApiController [Authorize]
  → IServerHubClient.SendPumpCommandAsync()         [Koneksi 2: Server→Simulator + 10s timeout]
    → SimulatorHub.ExecutePumpCommand()               [Simulator executes]
      → ISimulatorHubClient.PushPumpStateAsync()      [Koneksi 1: Simulator→Server via SendAsync]
        → VesselHub.SimPushPumpState()                [Server receives + validates input]
          → ILogService.LogPumpAsync()                 [Buffered write to SQLite]
          → Clients.Group("Dashboard").SendAsync()    [Koneksi 3a: Server→Browser]
            → dashboard.js "ReceivePumpState"         [UI updated!]
```

---

## 8. Data Transfer Objects (DTOs)

Semua data yang dikirim melalui SignalR menggunakan shared DTOs dari `MVCS.Shared`:

| DTO | Properties | Digunakan Untuk |
|-----|-----------|--------------------|
| `SimulationStateDto` | `IsGlobalRunning`, `IsCompassEnabled`, `IsWaterEnabled`, `IsPumpEnabled`, `IsLedEnabled`, `CompassIntervalMs`, `WaterIntervalMs` | Hardware state & toggle status |
| `CompassDto` | `Heading`, `CardinalDirection` | Data kompas |
| `WaterLevelDto` | `CurrentLevel`, `Status` | Level air + status (HIGH/NORMAL/LOW) |
| `PumpStateDto` | `IsOn`, `Message` | Status pompa |
| `LedStateDto` | `HexColor`, `Brightness` | Warna & kecerahan LED |

---

## 9. Background Workers (Sensor Broadcasters)

File: `MVCS.Simulator/Workers/CompassBroadcaster.cs` & `WaterBroadcaster.cs`

Workers berjalan sebagai `BackgroundService` dan terus mengirim data sensor secara periodik. Dependency inject via interface:

```csharp
public class CompassBroadcaster : BackgroundService
{
    private readonly ISimulationStateService _state;    // ← Interface (thread-safe)
    private readonly ISimulatorHubClient _hubClient;   // ← Interface
    private readonly IHubContext<SimulatorDashboardHub> _dashboardHub;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_state.State.IsGlobalRunning && _state.State.IsCompassEnabled)
            {
                // Simulasi drift
                _state.CompassHeading = (_state.CompassHeading + drift + 360) % 360;
                var cardinal = _state.GetCardinalDirection(_state.CompassHeading);

                // Push ke Server via SendAsync (Koneksi 1)
                await _hubClient.PushCompassAsync(_state.CompassHeading, cardinal);

                // Push ke local dashboard (Koneksi 3b)
                await _dashboardHub.Clients.All.SendAsync("ReceiveCompass", ...);
            }

            // Interval bisa diubah real-time via dashboard
            await Task.Delay(_state.CompassIntervalMs, stoppingToken);
        }
    }
}
```

**Interval default:**
- Compass: **500ms** (2x per detik)
- Water Level: **2000ms** (setiap 2 detik)
- Range configurable: **100ms – 10,000ms**

---

## 10. Auto-Reconnect & Fault Tolerance

### Startup Independence

Kedua app bisa dijalankan dalam **urutan apapun**. Masing-masing memiliki retry loop:

```
Skenario 1: Server duluan, Simulator belum menyala
  Server: IServerHubClient → retry ke (config URL) setiap 3s... gagal... gagal...
  (Simulator mulai)
  Server: Connected to Simulator ✓

Skenario 2: Simulator duluan, Server belum menyala
  Simulator: ISimulatorHubClient → retry ke (config URL) setiap 3s... gagal...
  (Server mulai)
  Simulator: Connected to Server ✓

Skenario 3: Salah satu mati saat running
  WithAutomaticReconnect → otomatis reconnect dengan backoff
  0s → 2s → 5s → 10s → 30s (max)
```

### Offline Detection (Server Dashboard)

```javascript
// Heartbeat check: jika tidak ada data dari simulator > 5 detik → offline
setInterval(() => {
    if (Date.now() - lastSimulatorUpdate > 5000) {
        // Tampilkan "SIMULATOR OFFLINE" overlay
        setHwBadge('compassHwBadge', 'compassOverlay', false, true);
    }
}, 1000);
```

### Connection State Tracking (Thread-Safe)

File: `MVCS.Server/Services/SimulatorConnectionService.cs` — implements `ISimulatorConnectionService`

```csharp
public class SimulatorConnectionService : ISimulatorConnectionService
{
    private readonly object _lock = new();           // ✅ Thread-safe
    private string? _simulatorConnectionId;
    private SimulationStateDto? _lastKnownState;

    public string? SimulatorConnectionId
    {
        get { lock (_lock) return _simulatorConnectionId; }
        set { lock (_lock) _simulatorConnectionId = value; }
    }

    public bool IsSimulatorConnected
    {
        get { lock (_lock) return _simulatorConnectionId != null; }
    }

    public SimulationStateDto? LastKnownState
    {
        get { lock (_lock) return _lastKnownState; }
        set { lock (_lock) _lastKnownState = value; }
    }
}
```

---

## 11. Best Practices yang Diterapkan

### 11.1 Interface-Based Dependency Injection

Semua service di-inject via interface, bukan concrete class:

| Interface | Implementation | Project |
|-----------|---------------|---------|
| `IServerHubClient` | `ServerHubClient` | Server |
| `ISimulatorConnectionService` | `SimulatorConnectionService` | Server |
| `ILogService` | `LogService` (buffered) | Server |
| `ISimulationStateService` | `SimulationStateService` | Simulator |
| `ISimulatorHubClient` | `SimulatorHubClient` | Simulator |

### 11.2 Configuration-Driven URLs

Semua URL diambil dari `appsettings.json`:

```json
// MVCS.Server/appsettings.json
{ "SignalR": { "SimulatorHubUrl": "http://localhost:5100/simulatorhub" } }

// MVCS.Simulator/appsettings.json
{ "SignalR": { "ServerHubUrl": "http://localhost:5000/vesselhub" } }
```

### 11.3 Thread Safety

`SimulationStateService` menggunakan lock pada **semua properties** — bukan hanya Toggle/SetInterval. Method `GetStateSnapshot()` membuat atomic copy untuk concurrent consumers.

### 11.4 Buffered Database Writes

`LogService` menggunakan `Channel<T>` untuk buffering, flush ke SQLite setiap 5 detik — mengurangi dari ~7,200 writes/jam menjadi ~720 writes/jam per sensor.

### 11.5 Input Validation di Hub

`VesselHub` memvalidasi semua data masuk dari Simulator:
- Compass heading: `0 ≤ heading < 360`
- Water level: `0 ≤ level ≤ 100`
- String params: null/empty check
- LED brightness: clamped `0-100`

### 11.6 Timeout pada Commands

Semua `InvokeAsync` calls (command ke Simulator) memiliki **10 detik timeout** via `CancellationTokenSource` — tidak bisa hang selamanya.

### 11.7 Fire-and-Forget Push

Push data sensor menggunakan `SendAsync` (bukan `InvokeAsync`) karena tidak butuh response — lebih efisien dan tidak memblok worker threads.

---

## 12. Sequence Diagrams

### Alur Data Sensor (Compass)

```
  CompassBroadcaster      ISimulatorHubClient      VesselHub           Dashboard
  (Background Worker)     (SignalR Client)          (SignalR Hub)       (Browser JS)
        │                       │                       │                    │
        │  Generate heading     │                       │                    │
        ├──────────────────────►│                       │                    │
        │  PushCompassAsync()   │                       │                    │
        │                       ├──SendAsync───────────►│                    │
        │                       │  SimPushCompass()      │                    │
        │                       │                       │ validate input     │
        │                       │                       │ buffer to Channel  │
        │                       │                       ├───────────────────►│
        │                       │                       │  ReceiveCompass    │
        │                       │                       │  (heading,cardinal)│
        │                       │                       │                    │  drawCompass()
        │  juga push ke lokal   │                       │                    │
        ├───────────────────────────────────────────────────────────────────►│
        │  SimulatorDashboardHub.ReceiveCompass          │                    │
```

### Alur Command (Pump)

```
  Browser       VesselApi        IServerHubClient    SimulatorHub      ISimulatorHubClient   VesselHub
  (JS)          Controller       (SignalR Client)    (SignalR Hub)     (SignalR Client)      (SignalR Hub)
    │               │                  │                   │                  │                   │
    │ POST /pump    │                  │                   │                  │                   │
    │ [Authorize]   │                  │                   │                  │                   │
    ├──────────────►│                  │                   │                  │                   │
    │               │ SendPumpCommand  │                   │                  │                   │
    │               ├─────────────────►│                   │                  │                   │
    │               │                  │ InvokeAsync       │                  │                   │
    │               │                  │ + 10s timeout     │                  │                   │
    │               │                  ├──────────────────►│                  │                   │
    │               │                  │                   │ PushPumpState    │                   │
    │               │                  │                   │ (SendAsync)      │                   │
    │               │                  │                   ├─────────────────►│                   │
    │               │                  │                   │                  │ SimPushPumpState   │
    │               │                  │                   │                  ├──────────────────►│
    │               │                  │                   │                  │                   │ validate
    │               │                  │                   │                  │                   │ buffer log
    │               │                  │   return result   │                  │   ReceivePumpState│
    │               │                  │◄──────────────────┤                  │──────────────────►│
    │               │◄─────────────────┤                   │                  │      (to browser) │
    │◄──────────────┤ JSON response    │                   │                  │                   │
    │ Update UI     │                  │                   │                  │                   │
```

---

## 13. File Reference Map

### Semua file yang terlibat dalam koneksi WebSocket:

```
MVCS.Shared/DTOs/
├── SimulationStateDto.cs      ← State global (toggles, intervals)
├── CompassDto.cs              ← Data kompas
├── WaterLevelDto.cs           ← Data level air
├── PumpStateDto.cs            ← Status pompa
└── LedStateDto.cs             ← Status LED

MVCS.Server/
├── Program.cs                 ← AddSignalR(), interface DI, health checks, global exception handler
├── appsettings.json           ← SignalR:SimulatorHubUrl, ConnectionStrings, SeedAdmin
├── Hubs/
│   └── VesselHub.cs           ← HUB: menerima data + input validation + buffered logging
├── Services/
│   ├── IServerHubClient.cs    ← INTERFACE: command abstraction
│   ├── ServerHubClient.cs     ← CLIENT: connect ke Simulator, kirim command + 10s timeout
│   ├── ISimulatorConnectionService.cs ← INTERFACE: connection tracking
│   ├── SimulatorConnectionService.cs  ← Track simulator online/offline (thread-safe)
│   ├── ILogService.cs         ← INTERFACE: logging abstraction
│   ├── LogService.cs          ← Buffered writes via Channel<T>, flush/5s
│   └── DataRetentionService.cs ← Cleanup records > 30 days
└── wwwroot/js/
    └── dashboard.js           ← BROWSER: connect ke /vesselhub, render UI real-time

MVCS.Simulator/
├── Program.cs                 ← AddSignalR(), interface DI, global exception handler
├── appsettings.json           ← SignalR:ServerHubUrl, Kestrel config
├── Hubs/
│   ├── SimulatorHub.cs        ← HUB: menerima command + execute + return result
│   └── SimulatorDashboardHub.cs ← HUB: lokal untuk browser simulator dashboard
├── Services/
│   ├── ISimulationStateService.cs ← INTERFACE: state management abstraction
│   ├── SimulationStateService.cs  ← Thread-safe state (full locking) + GetWaterStatus()
│   ├── ISimulatorHubClient.cs ← INTERFACE: data push abstraction
│   └── SimulatorHubClient.cs  ← CLIENT: connect ke Server, push data via SendAsync
├── Workers/
│   ├── CompassBroadcaster.cs  ← BackgroundService: generate & push compass data periodik
│   └── WaterBroadcaster.cs    ← BackgroundService: generate & push water level data periodik
└── wwwroot/js/
    └── simulator-dashboard.js ← BROWSER: connect ke /simulatordashboardhub, render UI
```

### Ringkasan SignalR Endpoints

| Endpoint URL | Di-host Oleh | Siapa yang Connect | Tujuan |
|-------------|-------------|-------------------|--------|
| `:5000/vesselhub` | Server (VesselHub) | ISimulatorHubClient + Browser JS | Terima data sensor + serve browser |
| `:5100/simulatorhub` | Simulator (SimulatorHub) | IServerHubClient | Terima command (+ timeout) |
| `:5100/simulatordashboardhub` | Simulator (SimulatorDashboardHub) | Browser JS lokal | Serve simulator dashboard |
| `:5000/health` | Server | Monitoring tools | Health check (SQLite) |

### Ringkasan SignalR Methods

**VesselHub (Server menerima dari Simulator — dengan input validation):**
| Method | Parameters | Validation | Fungsi |
|--------|-----------|------------|--------|
| `SimPushCompass` | `int heading, string cardinal` | `0 ≤ heading < 360`, non-empty cardinal | Terima data kompas → buffer log + forward |
| `SimPushWaterLevel` | `double level, string status` | `0 ≤ level ≤ 100`, non-empty status | Terima level air → buffer log + forward |
| `SimPushHardwareState` | `SimulationStateDto state` | null check | Terima state hardware → cache + forward |
| `SimPushPumpState` | `bool isOn, string message` | default message if empty | Terima status pompa → buffer log + forward |
| `SimPushLedState` | `string hexColor, int brightness` | default color, clamp brightness 0-100 | Terima status LED → buffer log + forward |

**SimulatorHub (Simulator menerima dari Server — via InvokeAsync + 10s timeout):**
| Method | Parameters | Return | Fungsi |
|--------|-----------|--------|--------|
| `ExecutePumpCommand` | `bool isOn, string message` | `object` (PumpStateDto/error) | Eksekusi command pompa |
| `ExecuteLedCommand` | `string hexColor, int brightness` | `object` (LedStateDto/error) | Eksekusi command LED |
| `ToggleHardware` | `string component` | `SimulationStateDto` | Toggle enable/disable hardware |
| `RequestState` | — | `SimulationStateDto` | Get current state (thread-safe snapshot) |


