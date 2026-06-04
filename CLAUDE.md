# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ServerPing is a lightweight Windows server monitoring tool that:
- Pings servers at a configurable interval (default 5s, range 1-300s)
- Sends Windows Toast notifications after a configurable number of consecutive failures (default 3, range 1-20)
- Provides a WPF GUI for managing the server list and monitoring settings
- Imports SSH profiles from Windows Terminal configuration
- Runs as a background service with system tray integration and minimal resource footprint

**Technology Stack:** .NET 9, C#

## Architecture

Single-entry dual-process design: the Service is the primary process; it owns the tray icon, starts monitoring, and launches the GUI on demand.

```
ServerPing.sln
├── ServerPing.Shared    (Class Library, net9.0)       — shared models and IPC protocol
├── ServerPing.Service   (Console App, net9.0-windows)  — background daemon, tray, IPC server
└── ServerPing.GUI       (WPF App, net9.0-windows)      — management panel (on-demand, exits fully when closed)
```

### ServerPing.Shared

| File | Purpose |
|------|---------|
| `Models/Server.cs` | Server entity: Id (Guid string), Name, Host, IsEnabled, Status, LastPingTime, ConsecutiveFailures |
| `Models/ServerStatus.cs` | Enum: Unknown, Online, Offline |
| `Models/ServerConfiguration.cs` | Config root: `List<Server>` + `MonitoringSettings` |
| `Models/MonitoringSettings.cs` | Configurable thresholds with clamping: PingIntervalSeconds (1-300), FailureThreshold (1-20), SilentStartup. Exposes `Clone()`. |
| `Models/ServerStats.cs` | Stats snapshot: `ServerStats { ServerId, LastHour, LastDay }`, each window is `PingStatsWindow { SuccessCount, FailureCount, AvailabilityPercent }` |
| `ConfigurationManager.cs` | Read/write `%APPDATA%\ServerPing\servers.json`. Returns empty config on failure. |
| `IPC/MessageType.cs` | IPC command enum (see IPC section below) |
| `IPC/IpcMessages.cs` | `IpcMessage`, `IpcResponse`, `AddServerRequest`, `RemoveServerRequest`, `UpdateSettingsRequest` |

### ServerPing.Service (always running)

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point. Initializes all services, wires events, conditionally launches GUI, calls `Application.Run()` for the WinForms message loop. |
| `PingService.cs` | Ping engine. Per-server `System.Threading.Timer` at configurable interval. Lock-protected state. 24h rolling history for stats. Exposes `Pause()`/`Resume()`. |
| `NotificationService.cs` | Windows Toast via `Microsoft.Toolkit.Uwp.Notifications`. Three notification types: offline, online, test. Offline notifications also play the Windows notification sound alias with a system-sound fallback. |
| `TrayService.cs` | `NotifyIcon` + `ContextMenuStrip`. Left-click opens GUI. Right-click shows live server list with 1h availability %. Pause/Resume toggle fires `MonitoringToggleRequested` event. |
| `IpcServer.cs` | Named Pipe server on `\\.\pipe\ServerPing`. Accepts one connection at a time; processes one JSON message per connection. |
| `GuiProcessManager.cs` | Finds / launches `ServerPing.GUI.exe`. `CloseGuiIfRunning()` tries graceful close then kills. |

### ServerPing.GUI (on-demand, fully exits when closed)

| File | Purpose |
|------|---------|
| `App.xaml` / `App.xaml.cs` | Single-instance Mutex guard. Dark theme resource dictionary (brushes, button/datagrid/textbox styles, CornerRadius=4). |
| `MainWindow.xaml` / `MainWindow.xaml.cs` | Frameless window with custom title bar. DataGrid with inline host editing, stats columns, action buttons. Settings (⚙) button opens `SettingsDialog`. |
| `SettingsDialog.xaml` / `SettingsDialog.xaml.cs` | Input validation for PingIntervalSeconds / FailureThreshold / SilentStartup. Test Notification and notification sound buttons. Calls `MainViewModel.SaveSettingsAsync`. |
| `ImportDialog.xaml` / `ImportDialog.xaml.cs` | SSH profile import: filters already-added hosts, Select All / Select None. |
| `ViewModels/ViewModelBase.cs` | `INotifyPropertyChanged` base with `SetProperty<T>`. |
| `ViewModels/RelayCommand.cs` | `ICommand` implementation supporting async delegates and CanExecute. |
| `ViewModels/MainViewModel.cs` | MVVM brain. 3-second `DispatcherTimer` refresh loop. All IPC calls routed through `IpcClient`. |
| `ViewModels/ServerViewModel.cs` | Per-row ViewModel. Wraps `Server` + `ServerStats`. Computed properties: `StatusText`, `LastPingTimeText`, `LastHourStatsText`, `LastHourAvailabilityText`. |
| `ViewModels/SshProfileViewModel.cs` | Wraps `SshProfile` with `IsSelected` for import dialog binding. |
| `Services/IpcClient.cs` | Named Pipe client. 3-second connect timeout. All methods async. |
| `Services/WindowsTerminalParser.cs` | Parses `%LOCALAPPDATA%\Packages\Microsoft.WindowsTerminal_*/LocalState/settings.json`. Extracts SSH profiles via regex. |
| `Converters/EnableToggleConverter.cs` | `true` → "禁用", `false` → "启用". |

## IPC Protocol

Named Pipe `\\.\pipe\ServerPing`, JSON, one request/response per connection:

- **Request:** `IpcMessage { Type: MessageType, Data: object? }`
- **Response:** `IpcResponse { Success: bool, ErrorMessage?: string, Data?: object }`

| MessageType | Data In | Data Out |
|-------------|---------|----------|
| `GetServers` | — | `List<Server>` |
| `UpdateServers` | `List<Server>` | — |
| `AddServer` | `AddServerRequest { Name, Host }` | `Server` (newly created) |
| `RemoveServer` | `RemoveServerRequest { ServerId }` | — |
| `GetStatus` | — | `{ OnlineCount, TotalCount }` |
| `GetSettings` | — | `MonitoringSettings` |
| `UpdateSettings` | `UpdateSettingsRequest { Settings }` | `MonitoringSettings` (saved) |
| `TestNotification` | — | — |
| `TestNotificationSound` | — | — |
| `GetServerStats` | — | `List<ServerStats>` |

**Serialization note:** `IpcResponse.Data` is `object?`. Both sides do a double round-trip: `JsonSerializer.Serialize(data)` → `JsonSerializer.Deserialize<T>(json)` to convert the `JsonElement` back to a typed object.

## Configuration

- Location: `%APPDATA%\ServerPing\servers.json`
- Format: `ServerConfiguration { Servers: List<Server>, Settings: MonitoringSettings }`
- Written by Service (via IpcServer handlers on every mutation). Read on startup.
- GUI never reads the file directly — always goes through IPC.

Windows Terminal settings location:
- `%LOCALAPPDATA%\Packages\Microsoft.WindowsTerminal_*/LocalState/settings.json`

## Key Design Decisions

- **Dual-process:** Service stays resident for monitoring; GUI is launched on demand and exits fully. Keeps idle memory low.
- **Single Named Pipe (one connection at a time):** Sufficient since only one GUI instance can run (Mutex-guarded). Avoids complexity of concurrent IPC.
- **3-second GUI refresh:** `DispatcherTimer` on UI thread polls `GetServers` + `GetStats`. No push notifications from Service to GUI — acceptable latency.
- **Runtime state preservation:** `PingService.UpdateServers` only updates Name/Host/IsEnabled from incoming data; Status/LastPingTime/ConsecutiveFailures are preserved from in-memory state to avoid stale overwrites.
- **Pause/Resume:** `PingService.Pause()` stops all timers; `Resume()` restarts them. Wired from `TrayService.MonitoringToggleRequested`.

## Development Commands

```bash
# Build entire solution
dotnet build

# Run Service (console mode)
dotnet run --project ServerPing.Service

# Run GUI (Service must be running first)
dotnet run --project ServerPing.GUI

# Run tests
dotnet test

# Publish — self-contained (standalone, no .NET required)
dotnet publish ServerPing.Service -c Release -r win-x64 --self-contained -o publish/standalone
dotnet publish ServerPing.GUI    -c Release -r win-x64 --self-contained -o publish/standalone

# Publish — portable (framework-dependent, requires .NET 9 runtime)
dotnet publish ServerPing.Service -c Release -r win-x64 --no-self-contained -o publish/portable
dotnet publish ServerPing.GUI    -c Release -r win-x64 --no-self-contained -o publish/portable
```

### Publish

两个项目必须 publish 到同一目录（`-o`），因为 `GuiProcessManager` 在 Service 所在目录查找 `ServerPing.GUI.exe`。

| 方案 | 输出目录 | 体积 | 运行要求 |
|------|----------|------|----------|
| Self-contained (standalone) | `publish/` | ~150 MB | 无，包含 .NET 运行时 |
| Portable (framework-dependent) | `publish/` | ~2 MB | 目标机器已安装 .NET 9 |

两个方案的入口均为 `ServerPing.exe`，GUI 由 Service 按需启动。

## Current Status

All core features complete:
- ✅ Project structure and shared models
- ✅ Ping engine (configurable interval, configurable failure threshold)
- ✅ Windows Toast notifications (offline / recovery / test)
- ✅ Windows default notification sound for offline alerts + GUI sound test
- ✅ System tray icon with live server status and availability %
- ✅ Tray Pause/Resume monitoring toggle
- ✅ Named Pipe IPC (Service ↔ GUI)
- ✅ WPF management panel (MVVM, add/delete/enable-disable/real-time status)
- ✅ 1h / 24h ping statistics with availability %
- ✅ Settings dialog (ping interval, failure threshold, silent startup)
- ✅ Windows Terminal SSH Profile import
- ✅ GUI single-instance protection (Mutex)
- ✅ Single entry point (Service launches GUI, SilentStartup option)

## Project Constraints

- **Target Framework:** .NET 9
- **Platform:** Windows-only (Windows notifications, system tray, WPF)
- **Resource Usage:** Service must stay under 20MB memory when GUI is closed
- **Process Isolation:** GUI must fully exit when closed; system tray remains via Service
- **Do Not Build** for validation modifications.

You Must update CLAUDE.mdfile to track this repo's file structure, and build methods, whenever the architecture ,status, or test/build methods changes.
