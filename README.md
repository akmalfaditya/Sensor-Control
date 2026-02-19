# Marine Vessel Control System (MVCS)

Sistem simulasi IoT untuk kontrol kapal laut, dibangun dengan arsitektur **decoupled dual-backend** menggunakan .NET 8. Kedua backend berkomunikasi secara **bidirectional melalui SignalR** — masing-masing bertindak sebagai **listener** dan **broadcaster** secara independen. Kedua backend memiliki **dashboard visualisasi real-time** masing-masing.

---

## Arsitektur

```
┌─────────────────────────────────────────────────────────────────────────┐
│                   BROWSER (Server Dashboard :5000)                      │
│              SignalR JS Client → ws://localhost:5000/vesselhub          │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │
  ┌─────────────────────────────▼──────────────────────────┐
  │             MVCS.Server (:5000)                        │
  │            ASP.NET Core MVC                            │
  │                                                        │
  │  ┌─────────────┐    ┌──────────────────┐               │
  │  │  VesselHub  │    │  ServerHubClient  │              │
  │  │  (Listener) │    │  (Broadcaster)    │              │
  │  │  /vesselhub │    │  → :5100           │              │
  │  └──────▲──────┘    └────────┬──────────┘              │
  │         │                    │                         │
  │  Menerima data sensor   Mengirim command               │
  │  dari Simulator         ke Simulator                   │
  │                                                        │
  │  SQLite DB │ Identity Auth │ LogService                │
  └─────────────────────────────┬──────────────────────────┘
                                │
  ┌─────────────────────────────▼──────────────────────────┐
  │           MVCS.Simulator (:5100)                       │
  │          ASP.NET Core MVC + Web API (Hybrid)           │
  │                                                        │
  │  ┌───────────────┐  ┌──────────────────────┐           │
  │  │ SimulatorHub  │  │ SimulatorHubClient   │           │
  │  │  (Listener)   │  │  (Broadcaster)       │           │
  │  │ /simulatorhub │  │  → :5000              │           │
  │  └──────▲────────┘  └────────┬─────────────┘           │
  │         │                    │                         │
  │  Menerima command       Push data sensor               │
  │  dari Server            ke Server                      │
  │                                                        │
  │  ┌──────────────────────────┐                          │
  │  │ SimulatorDashboardHub   │ ← Browser lokal (:5100)  │
  │  │ /simulatordashboardhub  │                           │
  │  └──────────────────────────┘                          │
  │                                                        │
  │  CompassBroadcaster │ WaterBroadcaster                 │
  │  SimulationStateService (interval & state mgmt)        │
  └────────────────────────────────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────────────────┐
│                 BROWSER (Simulator Dashboard :5100)                     │
│        SignalR JS Client → ws://localhost:5100/simulatordashboardhub    │
└─────────────────────────────────────────────────────────────────────────┘
```

**Tidak ada ketergantungan startup** — Server dan Simulator bisa dijalankan dalam urutan apapun. Masing-masing akan terus mencoba reconnect ke yang lain secara otomatis.

---

## Tech Stack

| Komponen | Teknologi |
|----------|-----------|
| Runtime | .NET 8 |
| Server Framework | ASP.NET Core MVC (Server), MVC + Web API Hybrid (Simulator) |
| Real-time | SignalR (server hub + client) di kedua backend |
| Database | SQLite + Entity Framework Core (Server only) |
| Autentikasi | Microsoft Identity (cookie-based, Server only) |
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
│       └── SimulationStateDto.cs     # IsGlobalRunning, Is*Enabled, intervals
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
└── MVCS.Simulator/                   # MVC + Web API Hybrid (port 5100)
    ├── Controllers/
    │   ├── HardwareController.cs     # GET/POST hardware state + broadcast
    │   ├── SimulationController.cs   # Toggle hardware, set intervals
    │   ├── HomeViewController.cs     # Landing page view controller
    │   └── DashboardViewController.cs # Dashboard view controller
    ├── Hubs/
    │   ├── SimulatorHub.cs           # SignalR hub (listener from Server)
    │   └── SimulatorDashboardHub.cs  # SignalR hub (local browser dashboard)
    ├── Services/
    │   ├── SimulationStateService.cs # Singleton state + interval management
    │   └── SimulatorHubClient.cs     # SignalR client (broadcaster ke Server)
    ├── Workers/
    │   ├── CompassBroadcaster.cs     # Background: compass (dynamic interval)
    │   └── WaterBroadcaster.cs       # Background: water level (dynamic interval)
    ├── Views/
    │   ├── Shared/_Layout.cshtml     # Dark theme layout (Tailwind CSS)
    │   ├── HomeView/Index.cshtml     # Simulator landing page
    │   ├── DashboardView/Index.cshtml # Hardware visualization dashboard
    │   ├── _ViewImports.cshtml
    │   └── _ViewStart.cshtml
    ├── wwwroot/
    │   └── js/simulator-dashboard.js # Dashboard frontend logic (SignalR + Canvas)
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
| `http://localhost:5000/Dashboard` | Server dashboard — monitoring & kontrol (perlu login) |
| `http://localhost:5100` | Landing page Simulator |
| `http://localhost:5100/Dashboard` | Simulator dashboard — hardware visualization & kontrol |
| `http://localhost:5100/swagger` | Swagger UI Simulator |

### Langkah 6: Login (Server)

Aplikasi otomatis membuat admin user saat pertama kali dijalankan:

| Field | Value |
|-------|-------|
| Email | `admin@mvcs.com` |
| Password | `Admin123` |

Atau buat akun baru via halaman Register.

> **Catatan:** Simulator dashboard tidak memerlukan login.

---

## Cara Kerja

### Alur Data Sensor (Simulator → Server → Dashboard)

1. `CompassBroadcaster` dan `WaterBroadcaster` generate data simulasi dengan **interval dinamis** (configurable via dashboard)
2. Workers memanggil `SimulatorHubClient.PushCompassAsync()` / `PushWaterLevelAsync()` → push ke **Server**
3. Workers juga push data ke `SimulatorDashboardHub` → update **Simulator dashboard** lokal
4. `VesselHub` di Server menyimpan log ke SQLite via `LogService`, lalu broadcast ke group "Dashboard"
5. Kedua `dashboard.js` (Server & Simulator) menerima event SignalR dan update UI real-time

### Alur Command (Dashboard → Server → Simulator)

1. User klik tombol pump/LED/toggle di Server dashboard
2. Dashboard POST ke `VesselApiController` (REST API)
3. `VesselApiController` memanggil `ServerHubClient.SendPumpCommandAsync()` dll.
4. `ServerHubClient` invoke method di `SimulatorHub` via SignalR — **method return value langsung**
5. `SimulatorHub` update state, lalu push result ke Server via `SimulatorHubClient` **dan** ke lokal via `SimulatorDashboardHub`
6. Kedua dashboard terupdate

### Alur Kontrol Lokal (Simulator Dashboard → Server)

1. User toggle pump/LED di **Simulator dashboard**
2. Dashboard POST ke `HardwareController` (REST API lokal)
3. `HardwareController` update state, push ke Server via `SimulatorHubClient`, dan broadcast ke `SimulatorDashboardHub`
4. Server dashboard juga terupdate via `VesselHub` broadcast

### Komunikasi SignalR (Bidirectional + Local)

```
Server                              Simulator
──────                              ─────────
VesselHub ◄──── push data ──────── SimulatorHubClient
  (listener)                         (broadcaster)

ServerHubClient ────── commands ──► SimulatorHub
  (broadcaster)                      (listener)

                                    SimulatorDashboardHub ──► Browser (:5100)
                                      (local dashboard hub)
```

Semua koneksi menggunakan **auto-reconnect** dengan backoff: 0s → 2s → 5s → 10s → 30s.

---

## Fitur Dashboard

### Server Dashboard (`localhost:5000/Dashboard`)

- **Compass** — Canvas real-time dengan heading dan arah kardinal
- **Water Tank** — Visualisasi level air dengan animasi gelombang SVG
- **Pump Control** — Toggle pump on/off, status aktif dengan animasi
- **LED Control** — Set warna HEX dan brightness, orb dengan efek glow
- **Per-Hardware Status** — Badge hijau (ON), merah (OFF), abu-abu (OFFLINE) per komponen
- **Hardware Toggle** — Klik badge untuk enable/disable masing-masing hardware
- **Simulator Offline Detection** — 5 detik tanpa update = otomatis tampilkan "SIMULATOR OFFLINE"
- **Log History** — Modal untuk Compass Log, Water History (chart), Pump Activity, LED Color Log
- **Bidirectional Sync** — Perubahan pump/LED dari Simulator dashboard langsung ter-reflect

### Simulator Dashboard (`localhost:5100/Dashboard`)

- **Compass** — Canvas gauge real-time dengan heading dan arah kardinal
- **Water Tank** — Visualisasi fill level dengan animasi gelombang dan status badge (NORMAL/HIGH/LOW)
- **Pump Control** — Toggle pump on/off langsung, broadcast otomatis ke Server
- **LED Control** — RGB color picker, brightness slider, glow orb, broadcast ke Server
- **Hardware Toggle** — Enable/disable per komponen via HW ON/OFF badge
- **Disabled Overlay** — Overlay visual saat hardware di-disable dengan tombol "Enable" cepat
- **Configurable Broadcast Interval** — Input field interval (100ms–10,000ms) untuk Compass dan Water sensor
- **Server Connection Status** — Badge Running/Standby menunjukkan status simulator

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
| POST | `/api/hardware/pump` | Set pump state (broadcast ke Server) |
| POST | `/api/hardware/led` | Set LED state (broadcast ke Server) |
| GET | `/api/simulation/state` | Get simulation state + intervals |
| POST | `/api/simulation/toggle/{component}` | Toggle hardware component |
| POST | `/api/simulation/interval/{component}` | Set broadcast interval (100–10,000ms) |

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
| Server dashboard menampilkan "SIMULATOR OFFLINE" | Pastikan Simulator sudah running di terminal terpisah |
| Simulator dashboard merah connection bar | Cek Simulator berjalan di port 5100 |
| Build error "SDK not found" | Install .NET 8 SDK, pastikan `dotnet --version` mengembalikan 8.x.x |
| Database locked | Stop kedua backend, hapus `mvcs.db`, jalankan ulang |
| SignalR reconnect terus-menerus | Cek firewall / antivirus tidak memblokir localhost WebSocket |
| Perubahan LED/Pump dari Simulator tidak muncul di Server | Pastikan kedua backend running dan connected (badge hijau) |

---

## Catatan Pengembangan

- **Tidak ada `docker-compose`** — jalankan langsung via `dotnet run`
- **Tidak ada HTTPS** — menggunakan HTTP untuk development (port 5000 & 5100)
- **Tailwind CSS via CDN** — tidak perlu build frontend terpisah
- **SQLite** — file-based, tidak perlu install database server
- **Auto-migrate** — schema database otomatis dibuat saat aplikasi start
- **Auto-seed** — akun admin otomatis dibuat jika belum ada
- **Dual Dashboard** — Server dan Simulator masing-masing punya dashboard sendiri
- **Bidirectional Sync** — perubahan dari salah satu dashboard langsung ter-sync ke yang lain
- **Dynamic Intervals** — interval broadcast sensor bisa diubah real-time via Simulator dashboard (100ms–10,000ms)
