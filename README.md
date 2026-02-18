# Marine Vessel Control System (MVCS)

Sistem simulasi IoT untuk kontrol kapal laut, dibangun dengan arsitektur **decoupled dual-backend** menggunakan .NET 8. Kedua backend berkomunikasi secara **bidirectional melalui SignalR** — masing-masing bertindak sebagai **listener** dan **broadcaster** secara independen.

---

## Arsitektur

```
┌─────────────────────────────────────────────────────────────────────┐
│                        BROWSER (Dashboard)                         │
│              SignalR JS Client → ws://localhost:5000/vesselhub      │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
           ┌─────────────────────▼────────────────────────┐
           │           MVCS.Server (:5000)                │
           │          ASP.NET Core MVC                    │
           │                                              │
           │  ┌─────────────┐    ┌──────────────────┐     │
           │  │  VesselHub  │    │  ServerHubClient  │    │
           │  │  (Listener) │    │  (Broadcaster)    │    │
           │  │  /vesselhub │    │  → :5100           │    │
           │  └──────▲──────┘    └────────┬──────────┘    │
           │         │                    │               │
           │  Menerima data sensor   Mengirim command     │
           │  dari Simulator         ke Simulator         │
           │                                              │
           │  SQLite DB │ Identity Auth │ LogService      │
           └─────────────────────────────┬────────────────┘
                                         │
           ┌─────────────────────────────▼────────────────┐
           │         MVCS.Simulator (:5100)               │
           │          ASP.NET Core Web API                │
           │                                              │
           │  ┌───────────────┐  ┌──────────────────────┐ │
           │  │ SimulatorHub  │  │ SimulatorHubClient   │ │
           │  │  (Listener)   │  │  (Broadcaster)       │ │
           │  │ /simulatorhub │  │  → :5000              │ │
           │  └──────▲────────┘  └────────┬─────────────┘ │
           │         │                    │               │
           │  Menerima command       Push data sensor     │
           │  dari Server            ke Server            │
           │                                              │
           │  CompassBroadcaster │ WaterBroadcaster       │
           │  SimulationStateService                      │
           └──────────────────────────────────────────────┘
```

**Tidak ada ketergantungan startup** — Server dan Simulator bisa dijalankan dalam urutan apapun. Masing-masing akan terus mencoba reconnect ke yang lain secara otomatis.

---

## Tech Stack

| Komponen | Teknologi |
|----------|-----------|
| Runtime | .NET 8 |
| Server Framework | ASP.NET Core MVC (Server), Web API (Simulator) |
| Real-time | SignalR (server hub + client) di kedua backend |
| Database | SQLite + Entity Framework Core |
| Autentikasi | Microsoft Identity (cookie-based) |
| Frontend | Tailwind CSS (CDN), Chart.js, SweetAlert2, SignalR JS |
| API Docs | Swagger/Swashbuckle (Simulator only) |

---

## Struktur Proyek

```
Sensor Control/
├── MVCS.sln                          # Solution file
│
├── MVCS.Shared/                      # Class library (shared DTOs)
│   └── DTOs/
│       ├── CompassDto.cs             # Heading, CardinalDirection
│       ├── WaterLevelDto.cs          # CurrentLevel, Status
│       ├── PumpStateDto.cs           # IsOn, Message
│       ├── LedStateDto.cs            # HexColor, Brightness
│       └── SimulationStateDto.cs     # IsGlobalRunning, Is*Enabled flags
│
├── MVCS.Server/                      # MVC Server (port 5000)
│   ├── Controllers/
│   │   ├── AccountController.cs      # Login / Register / Logout
│   │   ├── DashboardController.cs    # [Authorize] → Dashboard view
│   │   ├── HomeController.cs         # Landing page
│   │   └── VesselApiController.cs    # REST API (dashboard ↔ server)
│   ├── Data/
│   │   └── ApplicationDbContext.cs   # IdentityDbContext + log tables
│   ├── Hubs/
│   │   └── VesselHub.cs              # SignalR hub (listener from Simulator)
│   ├── Migrations/                   # EF Core migrations
│   ├── Models/
│   │   ├── AccountViewModels.cs      # Login/Register view models
│   │   ├── ErrorViewModel.cs
│   │   └── LogModels.cs              # CompassLog, WaterLog, PumpLog, LedLog
│   ├── Services/
│   │   ├── LogService.cs             # Read/write log data ke SQLite
│   │   ├── ServerHubClient.cs        # SignalR client (broadcaster ke Simulator)
│   │   └── SimulatorConnectionService.cs  # Track simulator presence
│   ├── Views/                        # Razor views (Account, Dashboard, Home, Shared)
│   ├── wwwroot/
│   │   ├── css/site.css
│   │   └── js/dashboard.js           # Dashboard frontend logic
│   └── Program.cs
│
└── MVCS.Simulator/                   # Web API Simulator (port 5100)
    ├── Controllers/
    │   ├── HardwareController.cs     # GET/POST hardware state (Swagger testing)
    │   └── SimulationController.cs   # Toggle hardware via Swagger
    ├── Hubs/
    │   └── SimulatorHub.cs           # SignalR hub (listener from Server)
    ├── Services/
    │   ├── SimulationStateService.cs # Singleton simulation state
    │   └── SimulatorHubClient.cs     # SignalR client (broadcaster ke Server)
    ├── Workers/
    │   ├── CompassBroadcaster.cs     # Background: simulate compass heading
    │   └── WaterBroadcaster.cs       # Background: simulate water level
    └── Program.cs
```

---

## Prerequisites

1. **.NET 8 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
   ```powershell
   # Verifikasi instalasi
   dotnet --version
   # Output harus 8.x.x
   ```

2. **Git** (opsional, untuk clone repository)

3. **IDE** (opsional)
   - Visual Studio 2022 (17.8+)
   - Visual Studio Code + C# Dev Kit extension
   - JetBrains Rider

---

## Build dari Scratch

### Langkah 1: Clone / Download Project

```powershell
# Jika menggunakan git
git clone <repository-url>
cd "Sensor Control"
```

Atau extract ZIP ke folder yang diinginkan.

### Langkah 2: Restore Dependencies

```powershell
dotnet restore MVCS.sln
```

Ini akan mengunduh semua NuGet packages:
- **MVCS.Server**: Identity, EF Core SQLite, SignalR Client
- **MVCS.Simulator**: SignalR Client, Swashbuckle (Swagger)
- **MVCS.Shared**: (tidak ada dependency eksternal)

### Langkah 3: Build Solution

```powershell
dotnet build MVCS.sln
```

Pastikan output: `Build succeeded. 0 Warning(s) 0 Error(s)`

### Langkah 4: Jalankan Kedua Backend

Buka **2 terminal** terpisah:

**Terminal 1 — Server (port 5000):**
```powershell
cd MVCS.Server
dotnet run
```

**Terminal 2 — Simulator (port 5100):**
```powershell
cd MVCS.Simulator
dotnet run
```

> **Urutan tidak penting!** Kedua backend akan auto-reconnect ke satu sama lain.

### Langkah 5: Akses Aplikasi

| URL | Keterangan |
|-----|-----------|
| `http://localhost:5000` | Landing page Server |
| `http://localhost:5000/Account/Login` | Login page |
| `http://localhost:5000/Dashboard` | Real-time dashboard (perlu login) |
| `http://localhost:5100/swagger` | Swagger UI Simulator |

### Langkah 6: Login

Aplikasi otomatis membuat admin user saat pertama kali dijalankan:

| Field | Value |
|-------|-------|
| Email | `admin@mvcs.com` |
| Password | `Admin123` |

Atau buat akun baru via halaman Register.

---

## Cara Kerja

### Alur Data Sensor (Simulator → Server → Dashboard)

1. `CompassBroadcaster` (setiap 500ms) dan `WaterBroadcaster` (setiap 2s) generate data simulasi
2. Workers memanggil `SimulatorHubClient.PushCompassAsync()` / `PushWaterLevelAsync()`
3. `SimulatorHubClient` mengirim data ke `VesselHub` di Server via SignalR
4. `VesselHub` menyimpan log ke SQLite via `LogService`, lalu broadcast ke group "Dashboard"
5. `dashboard.js` menerima event SignalR dan update UI real-time

### Alur Command (Dashboard → Server → Simulator)

1. User klik tombol pump/LED/toggle di dashboard
2. Dashboard POST ke `VesselApiController` (REST API)
3. `VesselApiController` memanggil `ServerHubClient.SendPumpCommandAsync()` dll.
4. `ServerHubClient` invoke method di `SimulatorHub` via SignalR — **method return value langsung** (tidak perlu correlation ID)
5. `SimulatorHub` update state, lalu push result balik ke Server via `SimulatorHubClient`
6. `VesselHub` broadcast update ke Dashboard

### Komunikasi SignalR (Bidirectional)

```
Server                              Simulator
──────                              ─────────
VesselHub ◄──── push data ──────── SimulatorHubClient
  (listener)                         (broadcaster)

ServerHubClient ────── commands ──► SimulatorHub
  (broadcaster)                      (listener)
```

Kedua arah menggunakan **auto-reconnect** dengan backoff: 0s → 2s → 5s → 10s → 30s.

---

## Fitur Dashboard

- **Compass** — Canvas real-time dengan heading dan arah kardinal
- **Water Tank** — Visualisasi level air dengan animasi gelombang SVG
- **Pump Control** — Toggle pump on/off, status aktif dengan animasi
- **LED Control** — Set warna HEX dan brightness, orb dengan efek glow
- **Per-Hardware Status** — Badge hijau (ON), merah (OFF), abu-abu (OFFLINE) per komponen
- **Hardware Toggle** — Klik badge untuk enable/disable masing-masing hardware
- **Simulator Offline Detection** — 5 detik tanpa update = otomatis tampilkan "SIMULATOR OFFLINE"
- **Log History** — Modal untuk Compass Log, Water History (chart), Pump Activity, LED Color Log
- **Log Accessible Saat Disabled** — Tombol log tetap bisa diklik walau hardware disabled/offline

---

## Endpoints API

### Server (`http://localhost:5000`)

| Method | Endpoint | Keterangan |
|--------|----------|-----------|
| POST | `/api/vessel/pump` | Set pump on/off |
| POST | `/api/vessel/led` | Set LED color & brightness |
| POST | `/api/vessel/toggle/{component}` | Toggle hardware (compass/water/pump/led) |
| GET | `/api/vessel/simulator/state` | Get current simulation state |
| GET | `/api/vessel/history/water` | Water level history (50 latest) |
| GET | `/api/vessel/history/pump` | Pump activity log (50 latest) |
| GET | `/api/vessel/history/compass` | Compass heading log (50 latest) |
| GET | `/api/vessel/history/led` | LED color log (50 latest) |

### Simulator (`http://localhost:5100`)

| Method | Endpoint | Keterangan |
|--------|----------|-----------|
| GET | `/api/hardware/compass` | Get current compass reading |
| GET | `/api/hardware/waterlevel` | Get current water level |
| POST | `/api/hardware/pump` | Set pump state directly |
| POST | `/api/hardware/led` | Set LED state directly |
| GET | `/api/simulation/state` | Get simulation state |
| POST | `/api/simulation/toggle/{component}` | Toggle hardware component |

---

## Database

SQLite database (`mvcs.db`) otomatis dibuat saat pertama kali Server dijalankan via EF Core Auto-Migrate.

**Tabel:**
- `AspNetUsers`, `AspNetRoles`, dll. — Identity tables
- `CompassLogs` — Heading, CardinalDirection, Timestamp
- `WaterLevelLogs` — Level, Status, Timestamp
- `PumpLogs` — IsOn, Message, Timestamp
- `LedLogs` — HexColor, Timestamp

Untuk reset database, hapus file `MVCS.Server/mvcs.db` dan restart Server.

---

## Troubleshooting

| Masalah | Solusi |
|---------|--------|
| Port 5000/5100 sudah dipakai | Matikan proses yang menggunakan port tersebut, atau ubah port di `Program.cs` (`builder.WebHost.UseUrls(...)`) |
| Dashboard menampilkan "SIMULATOR OFFLINE" | Pastikan Simulator sudah running di terminal terpisah |
| Build error "SDK not found" | Install .NET 8 SDK, pastikan `dotnet --version` mengembalikan 8.x.x |
| Database locked | Stop kedua backend, hapus `mvcs.db`, jalankan ulang |
| SignalR reconnect terus-menerus | Cek firewall / antivirus tidak memblokir localhost WebSocket |

---

## Catatan Pengembangan

- **Tidak ada `docker-compose`** — jalankan langsung via `dotnet run`
- **Tidak ada HTTPS** — menggunakan HTTP untuk development (port 5000 & 5100)
- **Tailwind CSS via CDN** — tidak perlu build frontend terpisah
- **SQLite** — file-based, tidak perlu install database server
- **Auto-migrate** — schema database otomatis dibuat saat aplikasi start
- **Auto-seed** — akun admin otomatis dibuat jika belum ada
