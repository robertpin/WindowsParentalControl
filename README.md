# Parental Control for Windows

> **Vibe Coded Disclaimer**
>
> This application was entirely vibe coded using **Claude Code with Claude Opus 4.6 (High)** and should be treated as such. The code was generated through conversational AI-assisted development — no line was hand-written. While functional and tested on the developer's machine, it has not undergone formal code review, security audit, or extensive QA. Use at your own discretion, review the source before deploying in any sensitive environment, and expect rough edges.

---

## What Is This?

Parental Control is a **Windows desktop application** that lets administrators enforce screen time limits and usage schedules on local Windows user accounts. It consists of two components:

- **Admin UI** — a WPF desktop app where you configure per-user time limits and schedules, view usage, and monitor events.
- **Background Service** — a Windows Service that runs silently, tracks active sessions, enforces limits, and forcefully logs users off when they exceed their allowed time or fall outside their permitted schedule.

---

## Features

### User Management
- Automatically discovers all local Windows user accounts
- Filters out built-in system accounts (DefaultAccount, WDAGUtilityAccount, Guest)
- Distinguishes between standard users and administrators
- Administrator accounts are protected — they cannot be restricted (displayed as read-only in the UI)

### Daily Time Limits
- Set a maximum number of minutes per day for each user (default: 120 minutes)
- Usage is tracked in real time, accumulating every 60 seconds
- Resets automatically at midnight

### Schedule Windows
- Define allowed hours for each user (e.g., 08:00 to 22:00)
- Users are forcefully logged off if they are still logged in when their schedule window closes
- Login attempts outside the schedule window are denied

### Automatic Enforcement
- The background service checks all active sessions every 60 seconds
- If a user exceeds their daily limit, they are immediately logged off
- If a user is logged in outside their allowed schedule, they are immediately logged off
- Login attempts are blocked if the user has already exhausted their daily limit or is outside their schedule

### Event & Audit Logging
All enforcement actions are logged with timestamps, user SIDs, and details:

| Event Type | Description |
|---|---|
| `LOGIN` | User logged in successfully |
| `LOGOUT` | User logged out |
| `SLEEP` | System entered sleep mode |
| `WAKE` | System resumed from sleep |
| `LIMIT_REACHED` | Daily usage limit was reached |
| `FORCED_LOGOUT` | User was forcefully logged off |
| `LOGIN_DENIED` | Login attempt was rejected |

### Sleep/Wake Awareness
- Usage tracking pauses when the system enters sleep mode
- Resumes accurately on wake — sleep time is not counted against the user
- All active session times are flushed to the database before sleep

### Service Status Monitoring
- The admin UI polls the Windows Service status every 5 seconds
- Displays a green indicator when the service is running, red when it's down

---

## Architecture

```
+---------------------------+       +---------------------------+
|   ParentalControl.Admin   |       | ParentalControl.Service   |
|   (WPF Desktop App)       |       | (Windows Service)         |
|                           |       |                           |
|  - Dashboard View         |       |  - Session Tracker        |
|  - User Detail View       |       |  - Usage Monitor Worker   |
|  - Service Status Monitor |       |  - Session Change Handler |
+------------+--------------+       +------------+--------------+
             |                                   |
             |         Shared Library            |
             +--------->  ParentalControl.Core <-+
                        |                      |
                        |  - Data Models       |
                        |  - Repositories      |
                        |  - Database Manager  |
                        |  - Session Manager   |
                        |  - Native Methods    |
                        +----------+-----------+
                                   |
                            SQLite Database
                   (C:\ProgramData\ParentalControl\data.db)
```

Both the Admin UI and the Service share the same SQLite database. When you change a user's limits in the Admin UI, the Service picks up those changes on its next 60-second tick.

---

## Tech Stack

| Component | Technology |
|---|---|
| UI Framework | WPF (Windows Presentation Foundation) |
| UI Theme | Material Design Themes for WPF (Indigo/Amber) |
| Architecture Pattern | MVVM (CommunityToolkit.Mvvm) |
| Backend Service | .NET Worker Service (Windows Service) |
| Database | SQLite (Microsoft.Data.Sqlite) |
| Logging | Serilog (rolling file, 30-day retention) |
| Platform Integration | Windows Terminal Services API (P/Invoke) |
| Installer | Inno Setup 6 |
| Target Framework | .NET 8.0 |
| Target OS | Windows (x64 only) |

---

## Project Structure

```
ParentalControl/
├── src/
│   ├── ParentalControl.Admin/           # WPF Admin UI
│   │   ├── App.xaml(.cs)                # App startup, Material Design theme config
│   │   ├── MainWindow.xaml(.cs)         # Main window with service status indicator
│   │   ├── Views/
│   │   │   ├── DashboardView.xaml(.cs)  # User list, admin list, recent events
│   │   │   └── UserDetailView.xaml(.cs) # Per-user limits config and event history
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs         # Service polling, view navigation
│   │   │   ├── DashboardViewModel.cs    # User discovery, event loading
│   │   │   ├── UserDetailViewModel.cs   # Limit editing, validation, save/remove
│   │   │   └── UserRow.cs              # Per-user presentation model
│   │   └── Services/
│   │       └── UserDiscovery.cs         # Windows local user enumeration
│   │
│   ├── ParentalControl.Core/            # Shared library
│   │   ├── Data/
│   │   │   ├── DatabaseManager.cs       # SQLite initialization and schema
│   │   │   ├── UserRepository.cs        # User CRUD
│   │   │   ├── LimitRepository.cs       # Limit config CRUD
│   │   │   ├── UsageRepository.cs       # Daily usage tracking
│   │   │   └── EventRepository.cs       # Event logging and queries
│   │   ├── Models/
│   │   │   ├── User.cs                  # User model (Id, Sid, Username, IsRestricted)
│   │   │   ├── LimitConfig.cs           # Limit model (DailyMinutes, ScheduleStart/End)
│   │   │   ├── UsageRecord.cs           # Usage model (UserId, Date, MinutesUsed)
│   │   │   ├── EventRecord.cs           # Event model (Timestamp, UserSid, EventType)
│   │   │   └── EventType.cs             # Event type enum
│   │   ├── Platform/
│   │   │   ├── SessionManager.cs        # WTS API wrapper (sessions, force logoff)
│   │   │   └── NativeMethods.cs         # P/Invoke for wtsapi32.dll
│   │   └── Logging/
│   │       └── LoggingConfig.cs         # Serilog configuration
│   │
│   └── ParentalControl.Service/         # Windows Service
│       ├── Program.cs                   # Host setup, DI registration
│       ├── ParentalControlServiceLifetime.cs  # Session change & power event handlers
│       ├── SessionTracker.cs            # Active session tracking & enforcement
│       └── UsageMonitorWorker.cs        # 60-second enforcement loop
│
├── installer/
│   ├── ParentalControl.iss              # Inno Setup installer script
│   └── Output/                          # Generated installer output
│
├── build.ps1                            # Build & package script
├── icon.ico                             # Application icon
├── ParentalControl.slnx                 # Solution file
└── .gitignore
```

---

## Getting Started

### Prerequisites

- **Windows 10/11** (x64)
- **.NET 8 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Inno Setup 6** (for building the installer) — [Download](https://jrsoftware.org/isdownload.php)

### Clone the Repository

```bash
git clone https://github.com/your-username/ParentalControl.git
cd ParentalControl
```

### Running in Development

To run the Admin UI directly during development:

```bash
dotnet run --project src/ParentalControl.Admin
```

To run the Service in console mode during development:

```bash
dotnet run --project src/ParentalControl.Service
```

> **Note:** The service needs to run with administrator privileges to track sessions and enforce logoffs. Right-click your terminal and "Run as Administrator" before starting the service.

### Building the Installer

The `build.ps1` script publishes both projects as self-contained win-x64 binaries and then invokes Inno Setup to create the installer:

```powershell
.\build.ps1
```

If the script fails with this error:

```
.\build.ps1 : File C:\Users\Robert\Desktop\ParentalControl\build.ps1 cannot be loaded because running scripts is
disabled on this system. For more information, see about_Execution_Policies at
https:/go.microsoft.com/fwlink/?LinkID=135170.
```

Run it with the execution policy bypassed:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\build.ps1
```

The build produces:
- `publish/service/` — Self-contained service binaries
- `publish/admin/` — Self-contained admin UI binaries
- `installer/Output/ParentalControlSetup.exe` — The installer

### Installing

Run `ParentalControlSetup.exe` as administrator. The installer will:

1. Install files to `C:\Program Files\ParentalControl\`
2. Register and start the `ParentalControl.Service` Windows Service (auto-start, runs as LocalSystem)
3. Configure automatic service restart on failure (10s, 30s, 60s intervals)
4. Create Start Menu and Desktop shortcuts for the Admin UI

### Uninstalling

Use "Add or Remove Programs" in Windows Settings, or run the uninstaller from the Start Menu. Uninstallation will:

- Stop and delete the Windows Service
- Remove all program files from `C:\Program Files\ParentalControl\`
- Remove the database and logs from `C:\ProgramData\ParentalControl\`

---

## How It Works

### Service Lifecycle

1. **Startup** — The service initializes the SQLite database, recovers any existing active sessions, and begins the 60-second monitoring loop.
2. **Session Change** — When a user logs on, the service checks their restrictions. If the user is outside their schedule or has exhausted their daily limit, login is denied (forced logoff). Otherwise, the session is tracked.
3. **Monitoring Loop** — Every 60 seconds, the service iterates over all active sessions. For each restricted user, it increments their daily usage and checks limits. Violations trigger a forced logoff.
4. **Logoff** — When a user logs off (or is forced off), accumulated session time is flushed to the database.
5. **Sleep/Wake** — On system sleep, all session times are flushed and tracking pauses. On wake, tracking resumes without counting sleep time.

### Data Flow

The Admin UI and the Service both read/write the same SQLite database at `C:\ProgramData\ParentalControl\data.db`. There is no API or IPC between them — the database is the shared state. SQLite's WAL (Write-Ahead Logging) mode ensures safe concurrent access.

### Database Schema

| Table | Purpose |
|---|---|
| `users` | Local Windows users (SID, username, restricted flag) |
| `limits` | Per-user limit config (daily minutes, schedule start/end) |
| `usage` | Daily usage records (user, date, minutes used) |
| `events` | Audit log of all enforcement events |

---

## File Locations (After Installation)

| Path | Contents |
|---|---|
| `C:\Program Files\ParentalControl\admin\` | Admin UI binaries |
| `C:\Program Files\ParentalControl\service\` | Service binaries |
| `C:\ProgramData\ParentalControl\data.db` | SQLite database |
| `C:\ProgramData\ParentalControl\logs\` | Rolling log files (30-day retention) |

---

## License

This project does not currently include a license. All rights reserved.
