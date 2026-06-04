# AGENTS.md

Guidelines for AI agents working in this repository.

## Project Snapshot

ServerPing — lightweight Windows server monitor. Two-process: a background **Service** (tray icon, ping engine, IPC server) and an on-demand **GUI** (WPF, exits when closed). Shares models via **ServerPing.Shared**.

Full architecture details are in [CLAUDE.md](CLAUDE.md).

## Build & Test

```bash
dotnet build                 # build all projects
dotnet test                  # run tests
dotnet run --project ServerPing.Service   # start service (console)
dotnet run --project ServerPing.GUI       # start GUI (service must be running)
```

Always run `dotnet build` after editing C# files to verify compilation.

## Code Conventions

- **Language:** C# 12 / .NET 9. Use `required`, primary constructors, collection expressions `[]` where appropriate.
- **Nullability:** nullable reference types enabled — annotate accordingly.
- **MVVM:** GUI uses strict MVVM. No logic in code-behind beyond event forwarding to ViewModel.
- **IPC:** all GUI→Service communication goes through `IpcClient` / `IpcServer`. Never read `servers.json` from GUI directly.
- **Thread safety:** `PingService` state is lock-protected. Do not call `PingService` methods from within a `lock (_lock)` block in another class.
- **No platform abstractions:** this is Windows-only by design. Do not add cross-platform shims.

## Key Invariants

1. `PingService.UpdateServers` must **preserve runtime state** (Status, LastPingTime, ConsecutiveFailures) for existing servers — only Name/Host/IsEnabled may be overwritten from incoming data.
2. `PingService.Pause()` stops all timers; `Resume()` restarts them. Tray toggle is wired to these.
3. GUI is single-instance (Mutex `ServerPing.GUI.SingleInstance`). Service detects it via `Process.GetProcessesByName`.
4. `ServerConfiguration` (including `MonitoringSettings`) is always persisted by the IpcServer on every mutation; never saved client-side.

## IPC Protocol Summary

Pipe: `\\.\pipe\ServerPing` — one JSON message per connection, then disconnect.

Request: `IpcMessage { Type: MessageType, Data: object? }`  
Response: `IpcResponse { Success: bool, ErrorMessage?: string, Data?: object? }`

Supported types: `GetServers`, `UpdateServers`, `AddServer`, `RemoveServer`, `GetStatus`, `GetSettings`, `UpdateSettings`, `TestNotification`, `GetServerStats`.

## What NOT to Do

- Do not push directly to `main` — use a branch and PR.
- Do not merge projects into one executable without discussion; the dual-process split is intentional.
- Do not add cross-platform dependencies (Linux notifications, etc.).
- Do not persist stats to disk — the 24h in-memory ring is intentional for low resource usage.
