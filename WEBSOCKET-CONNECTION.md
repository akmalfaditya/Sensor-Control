# 🔌 WebSocket Connection Guide — MVCS SignalR Architecture

Dokumentasi lengkap cara kerja koneksi WebSocket (SignalR) antara **MVCS.Simulator** dan **MVCS.Server**.

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
11. [Sequence Diagrams](#11-sequence-diagrams)
12. [File Reference Map](#12-file-reference-map)

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
│   │  VesselHub ◄────────┼───┤ SimulatorHubClient       │  Koneksi 1           │
│   │  (receives data)    │   │ (pushes sensor data)     │  DATA PUSH           │
│   │                     │   │                          │                      │
│   │  ServerHubClient ───┼──►│ SimulatorHub             │  Koneksi 2           │
│   │  (sends commands)   │   │ (receives commands)      │  COMMAND             │
│   └─────────────────────┘   └──────────────────────────┘                      │
│                                                                              │
│   Koneksi 1: Simulator pushes data sensor → Server (one-way stream)          │
│   Koneksi 2: Server sends commands → Simulator (request-response)            │
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
| **InvokeAsync** | Client memanggil method di Hub, **menunggu return value** | `SendPumpCommandAsync()` |
| **SendAsync** | Hub mengirim pesan ke semua/grup client, **fire-and-forget** | `Clients.Group("Dashboard").SendAsync(...)` |
| **Group** | Logical grouping dari connections | `"Dashboard"`, `"Simulator"` |
| **HostedService** | Background service yang auto-start saat aplikasi run | `SimulatorHubClient : IHostedService` |

### Hub vs HubConnection

```
Hub (Server-side)                    HubConnection (Client-side)
─────────────────                    ─────────────────────────────
- Meng-host endpoint                 - Connect ke endpoint
- Menerima panggilan dari client     - Memanggil method di Hub
- Bisa broadcast ke semua client     - Menerima pesan dari Hub
- Tahu siapa yang connected          - Punya state: Connected/Reconnecting/Disconnected
```

---

## 3. Dual-Direction Connection Pattern

### Kenapa tiap app punya Hub DAN HubConnection?

```
MVCS.Server                              MVCS.Simulator
════════════                              ════════════════
Punya Hub:        VesselHub               Punya Hub:        SimulatorHub
                  (menerima data)                           (menerima command)

Punya Client:     ServerHubClient         Punya Client:     SimulatorHubClient
                  (mengirim command)                        (mengirim data)
```

Setiap aplikasi bertindak sebagai **listener** (Hub) sekaligus **broadcaster** (HubConnection) — ini yang membuat komunikasi **bidirectional**.

---

## 4. Step-by-Step: Setup SignalR di Program.cs

### Step 4.1: Server — `MVCS.Server/Program.cs`

```csharp
// ❶ Tambahkan SignalR server service
builder.Services.AddSignalR();

// ❷ Register client yang akan connect ke Simulator
//    Pattern: Singleton + HostedService = 1 instance, auto-start
builder.Services.AddSingleton<ServerHubClient>();
builder.Services.AddHostedService<ServerHubClient>(
    sp => sp.GetRequiredService<ServerHubClient>()
);

// ❸ Map hub endpoint (setelah app.Build())
app.MapHub<VesselHub>("/vesselhub");
```

**Penjelasan registration pattern:**
- `AddSingleton<ServerHubClient>()` → Buat 1 instance yang di-share ke seluruh app
- `AddHostedService<ServerHubClient>(...)` → Otomatis panggil `StartAsync()` saat app start
- Mengambil instance yang sama (`GetRequiredService`) bukan membuat baru

### Step 4.2: Simulator — `MVCS.Simulator/Program.cs`

```csharp
// ❶ Tambahkan SignalR server service
builder.Services.AddSignalR();

// ❷ Register state service (singleton — shared di seluruh app)
builder.Services.AddSingleton<SimulationStateService>();

// ❸ Register client yang akan connect ke Server
builder.Services.AddSingleton<SimulatorHubClient>();
builder.Services.AddHostedService<SimulatorHubClient>(
    sp => sp.GetRequiredService<SimulatorHubClient>()
);

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

File: `MVCS.Simulator/Services/SimulatorHubClient.cs`

```csharp
public class SimulatorHubClient : IHostedService
{
    private HubConnection? _hub;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // ❶ Build koneksi ke Server's VesselHub
        _hub = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/vesselhub?role=simulator")
            //          ▲ target URL                  ▲ query string identifier
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
            await PushHardwareStateAsync();       // Berhasil → kirim state awal
            return;                               // Keluar loop
        }
        catch (Exception ex)
        {
            // Gagal → tunggu 3 detik, coba lagi
            await Task.Delay(3000, ct);
        }
    }
}
```

**Pattern penting:** Simulator **tidak crash** jika Server belum siap. Ia terus retry setiap 3 detik.

### Step 5.3: Server Menerima Koneksi

File: `MVCS.Server/Hubs/VesselHub.cs`

```csharp
public override async Task OnConnectedAsync()
{
    // ❶ Cek query string untuk identifikasi role
    var role = Context.GetHttpContext()?.Request.Query["role"].ToString();

    if (role == "simulator")
    {
        // ❷ Simpan connection ID (untuk tracking online/offline)
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
```

### Step 5.4: Simulator Mengirim Data via Push Methods

```csharp
// Di SimulatorHubClient — memanggil method di VesselHub
public async Task PushCompassAsync(int heading, string cardinal)
{
    if (!IsConnected) return;                                    // Guard: skip jika offline
    await _hub!.InvokeAsync("SimPushCompass", heading, cardinal); // Panggil method di VesselHub
}
```

### Step 5.5: Server Menerima & Meneruskan ke Browser

```csharp
// Di VesselHub — dipanggil oleh SimulatorHubClient
public async Task SimPushCompass(int heading, string cardinal)
{
    await _logService.LogCompassAsync(heading, cardinal);                  // ❶ Simpan ke SQLite
    await Clients.Group("Dashboard").SendAsync("ReceiveCompass", heading, cardinal); // ❷ Forward ke browser
}
```

---

## 6. Koneksi 2: Server → Simulator (Command)

**Tujuan:** Server mengirimkan command kontrol (pump on/off, LED color, toggle hardware) ke Simulator.

### Step 6.1: Server Membangun Koneksi

File: `MVCS.Server/Services/ServerHubClient.cs`

```csharp
_hub = new HubConnectionBuilder()
    .WithUrl("http://localhost:5100/simulatorhub")  // Target: SimulatorHub
    .WithAutomaticReconnect(/* same backoff policy */)
    .Build();

_ = ConnectWithRetryAsync(cancellationToken);  // Non-blocking retry
```

### Step 6.2: Server Mengirim Command (Request-Response)

```csharp
// InvokeAsync<T> = panggil method DAN tunggu return value
public async Task<string> SendPumpCommandAsync(bool isOn, string message)
{
    if (!IsConnected)
        throw new InvalidOperationException("Not connected to Simulator");

    // Panggil ExecutePumpCommand di SimulatorHub, tunggu response
    var result = await _hub!.InvokeAsync<object>("ExecutePumpCommand", isOn, message);
    return JsonSerializer.Serialize(result);
}
```

**InvokeAsync vs SendAsync:**
| Method | Blocking? | Return Value? | Use Case |
|--------|-----------|---------------|----------|
| `InvokeAsync<T>` | Ya (await) | Ya | Command yang butuh response |
| `SendAsync` | Tidak | Tidak | Fire-and-forget broadcast |

### Step 6.3: Simulator Menerima & Mengeksekusi Command

File: `MVCS.Simulator/Hubs/SimulatorHub.cs`

```csharp
public async Task<object> ExecutePumpCommand(bool isOn, string message)
{
    // ❶ Guard: cek apakah hardware enabled
    if (!_state.State.IsPumpEnabled)
        return new { error = "Pump is disabled", disabled = true };

    // ❷ Update state lokal
    _state.PumpIsOn = isOn;
    var result = new PumpStateDto { IsOn = isOn, Message = "Pump activated" };

    // ❸ Push update BALIK ke Server via Koneksi 1
    await _hubClient.PushPumpStateAsync(result.IsOn, result.Message);

    // ❹ Push ke local dashboard (Koneksi 3b)
    await _dashboardHub.Clients.All.SendAsync("ReceivePumpState", result.IsOn, result.Message);

    // ❺ Return langsung ke Server sebagai response
    return result;
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

Browser **TIDAK** berkomunikasi langsung via SignalR untuk command. Sebaliknya, melalui **REST API**:

```javascript
// Browser → REST API → ServerHubClient → SimulatorHub
const res = await fetch('/api/vessel/pump', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ isOn: true, message: 'Manual ON' })
});
```

**Alur lengkap:**
```
Browser → POST /api/vessel/pump → VesselApiController
  → ServerHubClient.SendPumpCommandAsync()          [Koneksi 2: Server→Simulator]
    → SimulatorHub.ExecutePumpCommand()              [Simulator executes]
      → SimulatorHubClient.PushPumpStateAsync()      [Koneksi 1: Simulator→Server]
        → VesselHub.SimPushPumpState()               [Server receives]
          → Clients.Group("Dashboard").SendAsync()   [Koneksi 3a: Server→Browser]
            → dashboard.js "ReceivePumpState"        [UI updated!]
```

---

## 8. Data Transfer Objects (DTOs)

Semua data yang dikirim melalui SignalR menggunakan shared DTOs dari `MVCS.Shared`:

| DTO | Properties | Digunakan Untuk |
|-----|-----------|-----------------|
| `SimulationStateDto` | `IsGlobalRunning`, `IsCompassEnabled`, `IsWaterEnabled`, `IsPumpEnabled`, `IsLedEnabled`, `CompassIntervalMs`, `WaterIntervalMs` | Hardware state & toggle status |
| `CompassDto` | `Heading`, `CardinalDirection` | Data kompas |
| `WaterLevelDto` | `CurrentLevel`, `Status` | Level air + status (HIGH/NORMAL/LOW) |
| `PumpStateDto` | `IsOn`, `Message` | Status pompa |
| `LedStateDto` | `HexColor`, `Brightness` | Warna & kecerahan LED |

---

## 9. Background Workers (Sensor Broadcasters)

File: `MVCS.Simulator/Workers/CompassBroadcaster.cs` & `WaterBroadcaster.cs`

Workers berjalan sebagai `BackgroundService` dan terus mengirim data sensor secara periodik:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        if (_state.State.IsGlobalRunning && _state.State.IsCompassEnabled)
        {
            // Simulasi drift
            _state.CompassHeading = (_state.CompassHeading + drift + 360) % 360;

            // Push ke Server (Koneksi 1)
            await _hubClient.PushCompassAsync(_state.CompassHeading, cardinal);

            // Push ke local dashboard (Koneksi 3b)
            await _dashboardHub.Clients.All.SendAsync("ReceiveCompass", ...);
        }

        // Interval bisa diubah real-time via dashboard
        await Task.Delay(_state.CompassIntervalMs, stoppingToken);
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
  Server: ServerHubClient → retry ke :5100 setiap 3s... gagal... gagal...
  (Simulator mulai)
  Server: Connected to Simulator ✓

Skenario 2: Simulator duluan, Server belum menyala
  Simulator: SimulatorHubClient → retry ke :5000 setiap 3s... gagal...
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

### Connection State Tracking

File: `MVCS.Server/Services/SimulatorConnectionService.cs`

```csharp
public class SimulatorConnectionService
{
    public string? SimulatorConnectionId { get; set; }
    public bool IsSimulatorConnected => SimulatorConnectionId != null;
    public SimulationStateDto? LastKnownState { get; set; }  // Cache
}
```

---

## 11. Sequence Diagrams

### Alur Data Sensor (Compass)

```
  CompassBroadcaster      SimulatorHubClient       VesselHub           Dashboard
  (Background Worker)     (SignalR Client)          (SignalR Hub)       (Browser JS)
        │                       │                       │                    │
        │  Generate heading     │                       │                    │
        ├──────────────────────►│                       │                    │
        │  PushCompassAsync()   │                       │                    │
        │                       ├──────────────────────►│                    │
        │                       │  SimPushCompass()      │                    │
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
  Browser       VesselApi        ServerHubClient     SimulatorHub      SimulatorHubClient    VesselHub
  (JS)          Controller       (SignalR Client)    (SignalR Hub)     (SignalR Client)      (SignalR Hub)
    │               │                  │                   │                  │                   │
    │ POST /pump    │                  │                   │                  │                   │
    ├──────────────►│                  │                   │                  │                   │
    │               │ SendPumpCommand  │                   │                  │                   │
    │               ├─────────────────►│                   │                  │                   │
    │               │                  │ ExecutePumpCommand │                  │                   │
    │               │                  ├──────────────────►│                  │                   │
    │               │                  │                   │ PushPumpState    │                   │
    │               │                  │                   ├─────────────────►│                   │
    │               │                  │                   │                  │ SimPushPumpState   │
    │               │                  │                   │                  ├──────────────────►│
    │               │                  │   return result   │                  │                   │
    │               │                  │◄──────────────────┤                  │   ReceivePumpState│
    │               │◄─────────────────┤                   │                  │──────────────────►│
    │◄──────────────┤ JSON response    │                   │                  │      (to browser) │
    │ Update UI     │                  │                   │                  │                   │
```

---

## 12. File Reference Map

### Semua file yang terlibat dalam koneksi WebSocket:

```
MVCS.Shared/DTOs/
├── SimulationStateDto.cs      ← State global (togles, intervals)
├── CompassDto.cs              ← Data kompas
├── WaterLevelDto.cs           ← Data level air
├── PumpStateDto.cs            ← Status pompa
└── LedStateDto.cs             ← Status LED

MVCS.Server/
├── Program.cs                 ← AddSignalR(), MapHub<VesselHub>, register ServerHubClient
├── Hubs/
│   └── VesselHub.cs           ← HUB: menerima data dari Simulator, forward ke browser
├── Services/
│   ├── ServerHubClient.cs     ← CLIENT: connect ke :5100/simulatorhub, kirim command
│   └── SimulatorConnectionService.cs  ← Track simulator online/offline + cache state
└── wwwroot/js/
    └── dashboard.js           ← BROWSER: connect ke /vesselhub, render UI real-time

MVCS.Simulator/
├── Program.cs                 ← AddSignalR(), MapHub x2, register SimulatorHubClient + Workers
├── Hubs/
│   ├── SimulatorHub.cs        ← HUB: menerima command dari Server, execute + return result
│   └── SimulatorDashboardHub.cs ← HUB: lokal untuk browser simulator dashboard
├── Services/
│   ├── SimulatorHubClient.cs  ← CLIENT: connect ke :5000/vesselhub, push sensor data
│   └── SimulationStateService.cs ← Singleton state management (heading, water, pump, LED)
├── Workers/
│   ├── CompassBroadcaster.cs  ← BackgroundService: generate & push compass data periodik
│   └── WaterBroadcaster.cs    ← BackgroundService: generate & push water level data periodik
└── wwwroot/js/
    └── simulator-dashboard.js ← BROWSER: connect ke /simulatordashboardhub, render UI
```

### Ringkasan SignalR Endpoints

| Endpoint URL | Di-host Oleh | Siapa yang Connect | Tujuan |
|-------------|-------------|-------------------|--------|
| `:5000/vesselhub` | Server (VesselHub) | SimulatorHubClient + Browser JS | Terima data sensor + serve browser |
| `:5100/simulatorhub` | Simulator (SimulatorHub) | ServerHubClient | Terima command |
| `:5100/simulatordashboardhub` | Simulator (SimulatorDashboardHub) | Browser JS lokal | Serve simulator dashboard |

### Ringkasan SignalR Methods

**VesselHub (Server menerima dari Simulator):**
| Method | Parameters | Fungsi |
|--------|-----------|--------|
| `SimPushCompass` | `int heading, string cardinal` | Terima data kompas → log + forward |
| `SimPushWaterLevel` | `double level, string status` | Terima level air → log + forward |
| `SimPushHardwareState` | `SimulationStateDto state` | Terima state hardware → cache + forward |
| `SimPushPumpState` | `bool isOn, string message` | Terima status pompa → log + forward |
| `SimPushLedState` | `string hexColor, int brightness` | Terima status LED → log + forward |

**SimulatorHub (Simulator menerima dari Server):**
| Method | Parameters | Return | Fungsi |
|--------|-----------|--------|--------|
| `ExecutePumpCommand` | `bool isOn, string message` | `object` (PumpStateDto/error) | Eksekusi command pompa |
| `ExecuteLedCommand` | `string hexColor, int brightness` | `object` (LedStateDto/error) | Eksekusi command LED |
| `ToggleHardware` | `string component` | `SimulationStateDto` | Toggle enable/disable hardware |
| `RequestState` | — | `SimulationStateDto` | Get current state |

---

*Dokumen ini dibuat berdasarkan analisis codebase MVCS pada 19 Februari 2026.*
