# AGENTS.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ServerPing is a lightweight Windows server monitoring tool that:
- Pings servers at a configurable interval (default 3s, range 1-300s)
- Sends Windows Toast notifications after a configurable number of consecutive failures (default 3, range 1-20)
- Provides a WPF GUI for managing the server list and monitoring settings
- Imports SSH profiles from Windows Terminal configuration
- Runs as a background service with system tray integration and minimal resource footprint

**Technology Stack:** .NET 9, C#

## Architecture

Single-entry dual-process design: the Service is the primary process; it owns the tray icon, starts monitoring, and launches the GUI on demand.

```
ServerPing.sln
├── .github/workflows/release.yml                   — tag-triggered GitHub Release asset build
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
| `Models/MonitoringSettings.cs` | Configurable thresholds with clamping: PingIntervalSeconds (default 3, range 1-300), FailureThreshold (default 3, range 1-20), SilentStartup, offline notification sound. Exposes `Clone()`. |
| `Models/ServerStats.cs` | Stats snapshot: `ServerStats { ServerId, LastHour, LastDay }`, each window is `PingStatsWindow { SuccessCount, FailureCount, AvailabilityPercent }` |
| `ConfigurationManager.cs` | Read/write `%APPDATA%\ServerPing\servers.json`. Returns empty config on failure. |
| `IPC/MessageType.cs` | IPC command enum (see IPC section below) |
| `IPC/IpcMessages.cs` | `IpcMessage`, `IpcResponse`, `AddServerRequest`, `RemoveServerRequest`, `UpdateSettingsRequest` |

### ServerPing.Service (always running)

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point. Initializes all services, wires events, conditionally launches GUI, calls `Application.Run()` for the WinForms message loop. |
| `PingService.cs` | Ping engine. Per-server `System.Threading.Timer` at configurable interval. Lock-protected state. Rolling stats and change events. Exposes `Pause()`/`Resume()`. Offline/tray-related state details are documented in `DOCS/tray-state-machine.md`. |
| `NotificationService.cs` | Windows Toast via `Microsoft.Toolkit.Uwp.Notifications`. Three notification types: offline, online, test. Offline alert sound can be played explicitly by service logic with bundled `offline.wav` and fallback. |
| `TrayService.cs` | `NotifyIcon` + `ContextMenuStrip`. Left-click opens GUI. Right-click shows live server list with 1h availability %. Alert icon behavior is summarized in Key Design Decisions and detailed in `DOCS/tray-state-machine.md`. |
| `IpcServer.cs` | Named Pipe server on `\\.\pipe\ServerPing`. Accepts one connection at a time; processes one JSON message per connection. |
| `GuiProcessManager.cs` | Finds / launches `ServerPing.GUI.exe`. `CloseGuiIfRunning()` tries graceful close then kills. |

### ServerPing.GUI (on-demand, fully exits when closed)

| File | Purpose |
|------|---------|
| `App.xaml` / `App.xaml.cs` | Single-instance Mutex guard. Theme resource dictionary (runtime light/dark brushes, shared button/datagrid/textbox styles, CornerRadius=4). |
| `MainWindow.xaml` / `MainWindow.xaml.cs` | Frameless shell window with custom title bar, tray toggle positioning, hibernation timer, and Settings (⚙) dialog launch. The server list UI is hosted through `Views/ServerList/ServerListView`. |
| `Views/ServerList/ServerListView.xaml` / `Views/ServerList/ServerListView.xaml.cs` | Server list component. Owns the DataGrid, inline name/host editing events, stats columns, host visibility button, name sort header, and drag/drop insertion indicator. |
| `Dialogs/SettingsDialog.xaml` / `Dialogs/SettingsDialog.xaml.cs` | Input validation for PingIntervalSeconds / FailureThreshold / SilentStartup and offline notification sound setting. Test Notification and notification sound buttons. Calls `MainViewModel.SaveSettingsAsync`. |
| `Dialogs/ImportDialog.xaml` / `Dialogs/ImportDialog.xaml.cs` | SSH profile import: filters already-added hosts, Select All / Select None. |
| `Dialogs/ThemedMessageBox.xaml` / `Dialogs/ThemedMessageBox.xaml.cs` | Theme-aware replacement for WPF `MessageBox` used by validation and information dialogs. |
| `Styles/ScrollBar.xaml` | Theme-aware global ScrollBar control template merged by `App.xaml`. |
| `Models/GuiState.cs` | GUI-only persisted state root, currently name sort mode plus custom server order IDs. |
| `Models/ServerSortMode.cs` | Enum for server list sorting: Auto, AToZ, ZToA. |
| `ViewModels/ViewModelBase.cs` | `INotifyPropertyChanged` base with `SetProperty<T>`. |
| `ViewModels/RelayCommand.cs` | `ICommand` implementation supporting async delegates and CanExecute. |
| `ViewModels/MainViewModel.cs` | MVVM brain. 3-second `DispatcherTimer` refresh loop, GUI sort state coordination, and all IPC calls routed through `IpcClient`. |
| `ViewModels/ServerViewModel.cs` | Per-row ViewModel. Wraps `Server` + `ServerStats`. Computed properties: `StatusText`, `LastPingTimeText`, `LastHourStatsText`, `LastHourAvailabilityText`. |
| `ViewModels/SshProfileViewModel.cs` | Wraps `SshProfile` with `IsSelected` for import dialog binding. |
| `Services/GuiStateStore.cs` | Read/write `%APPDATA%\ServerPing\gui-state.json` for GUI-only state. Failures are treated as non-critical. |
| `Services/IpcClient.cs` | Named Pipe client. 3-second connect timeout. All methods async. |
| `Services/ThemeMessageBox.cs` | Central API for showing `ThemedMessageBox` with localized text and active-window ownership. |
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
- GUI-only state: `%APPDATA%\ServerPing\gui-state.json`, written by the GUI through `GuiStateStore`. It stores presentation preferences such as server list name sort mode and custom server order IDs, separate from the service-owned server configuration.

Windows Terminal settings location:
- `%LOCALAPPDATA%\Packages\Microsoft.WindowsTerminal_*/LocalState/settings.json`

## Key Design Decisions

- **Dual-process:** Service stays resident for monitoring; GUI is launched on demand and exits fully. Keeps idle memory low.
- **Single Named Pipe (one connection at a time):** Sufficient since only one GUI instance can run (Mutex-guarded). Avoids complexity of concurrent IPC.
- **3-second GUI refresh:** `DispatcherTimer` on UI thread polls `GetServers` + `GetStats`. No push notifications from Service to GUI — acceptable latency.
- **GUI-only ordering state:** Server configuration remains service-owned and IPC-backed. Server list custom order and sort mode are presentation state persisted by the GUI in `gui-state.json`, so drag/drop ordering does not rewrite server identity/configuration order.
- **Runtime state preservation:** `PingService.UpdateServers` only updates Name/Host/IsEnabled from incoming data; Status/LastPingTime/ConsecutiveFailures are preserved from in-memory state to avoid stale overwrites.
- **Offline, sound, and tray alert state:** Each server enters `Offline` when its consecutive failures reach the user-configured threshold, and leaves `Offline` on a successful ping. Offline sound plays per server when it enters `Offline`. The tray alert icon is active whenever any enabled server is `Offline`. Detailed state variables, events, and transitions are documented in `DOCS/tray-state-machine.md`.
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

# Create GitHub Release assets (portable + standalone ZIPs; version comes from current git tag)
.\publish.ps1

# Create only the smallest portable ZIP
.\publish.ps1 -Mode portable

# Publish GitHub Release assets from CI
git tag v1.0.0
git push origin v1.0.0
```

### Publish

两个项目必须 publish 到同一目录（`-o`），因为 `GuiProcessManager` 在 Service 所在目录查找 `ServerPing.GUI.exe`。

正式 GitHub Release assets 使用根目录 `publish.ps1` 生成，输出到 `artifacts/`：

```powershell
.\publish.ps1                  # portable + standalone, version from current git tag
.\publish.ps1 -Mode portable
.\publish.ps1 -Mode standalone
.\publish.ps1 -Version v1.0.0  # manual version override
```

GitHub Actions 发布流程位于 `.github/workflows/release.yml`。推送 `v*` tag 时，workflow 在 `windows-latest` 上安装 .NET 9 SDK，调用 `.\publish.ps1 -Version <tag> -Mode all` 生成 portable 与 standalone ZIP，并用仓库 `GITHUB_TOKEN` 创建或更新同名 GitHub Release，将 `artifacts/*.zip` 上传为 Release assets。

## Current Status

All core features complete:
- ✅ Project structure and shared models
- ✅ Ping engine (configurable interval, configurable failure threshold)
- ✅ Windows Toast notifications (offline / recovery / test)
- ✅ Bundled offline alert sound with Windows/system fallback + GUI sound test
- ✅ System tray icon with live server status and availability %
- ✅ Tray alert icon based on enabled `Offline` servers, with per-server offline sound (see `DOCS/tray-state-machine.md`)
- ✅ Tray Pause/Resume monitoring toggle
- ✅ Named Pipe IPC (Service ↔ GUI)
- ✅ WPF management panel (MVVM, add/delete/enable-disable/real-time status)
- ✅ Server list name sorting (Auto / A-Z / Z-A) with GUI-persisted custom drag/drop order
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
- **UI Localization:** New or changed user-facing GUI text, tooltips, validation messages, and dialog content must use the existing localization flow (`LocalizationService` or `DynamicResource`) and update all language resource dictionaries under `ServerPing.GUI/Localization/`.
- **Do Not Build** for validation modifications.

You Must update AGENTS.md file to track this repo's file structure, and build methods, whenever the architecture ,status, or test/build methods changes.
