# Tray and Recovery State Machine

This document tracks the tray alert icon logic, alert sound logic, and the state variables that connect `PingService`, `Program.cs`, and `TrayService`.

## Responsibilities

| Module | Responsibility |
|--------|----------------|
| `PingService` | Owns server ping state, rolling statistics, and change events. Maintains per-server consecutive failure counts and last successful ping times. |
| `Program.cs` | Owns the service-level tray state machine. It listens to `PingService` events, decides when the tray icon changes, and plays the red-transition sound at the same transition. |
| `TrayService` | Owns the `NotifyIcon`, menu rendering, tooltip, and icon switching. It does not decide alert state; `Program.cs` passes the target tray state into `UpdateStatus(...)`. |
| `NotificationService` | Plays the bundled offline alert sound and Windows/system fallback sound. |

## Core Rule

The tray state has two modes: normal and red.

When the tray icon is normal:

- Watch each enabled server independently.
- If the same enabled server reaches `MonitoringSettings.FailureThreshold` consecutive failed probes, switch the tray to red.
- The red transition plays the offline alert sound at the same time when `MonitoringSettings.OfflineNotificationSoundEnabled` is enabled.

When the tray icon is red:

- Stop using the consecutive-failure entry rule as the main decision.
- Monitor every factor that can make the enabled server set healthy again.
- If every enabled server has been available within the last 1 minute, switch the tray back to normal.
- The normal transition does not play a sound.

Alert sound changes and tray icon changes must be synchronized. Sounds are transition effects, not independent status effects.

## State Variables

### `Server.ConsecutiveFailures`

Type:

```csharp
int
```

Purpose:

- Counts consecutive failed probes per server.
- Reset to `0` on a successful ping.
- Used only for the normal -> red transition.
- Also used by `PingService` to set `Server.Status = Offline` after the configured threshold.

### `PingService._lastSuccessfulPingTimes`

Type:

```csharp
Dictionary<string, DateTime>
```

Purpose:

- Stores the latest successful ping time per server.
- Used by `PingService.WasAvailableInLastMinute(serverId)`.
- Used only for the red -> normal transition.

Lifecycle:

- Updated on every successful ping.
- Removed when a server is removed through `UpdateServers()`.
- Preserved when server name, host, or enabled state changes.

### `Program.cs isTrayAlertActive`

Type:

```csharp
bool
```

Purpose:

- Represents the current tray icon state.
- `false` means normal icon (`app.ico`).
- `true` means red alert icon (`app-alert.ico`).
- Determines which transition rule applies next.

Startup:

- Initialized after `pingService.Start(...)`.
- Startup refresh uses `playTransitionSound: false`, so startup never plays recovery or alert sounds.

## Events That Refresh Tray State

`Program.cs` calls `RefreshTrayStatus(playTransitionSound: true)` for runtime changes that can affect the tray state:

| Event | Why it matters |
|-------|----------------|
| `PingService.PingResultRecorded` | Consecutive failures and last successful ping times may change. |
| `PingService.StatusChanged` | Server online/offline status changed. Also shows Toast notifications, but does not own tray sound timing. |
| `PingService.ServersChanged` | Add, delete, enable, disable, or edit server operations can change whether all enabled servers are available. |
| `PingService.SettingsChanged` | `FailureThreshold` changes can change whether a server crosses the normal -> red threshold. |

Startup calls `RefreshTrayStatus(playTransitionSound: false)`.

## Tray Icon State Machine

```text
normal
  when any single enabled server has ConsecutiveFailures >= FailureThreshold
    -> red
    -> play offline alert sound if enabled

red
  when every enabled server was available in the last 1 minute
    -> normal
    -> no sound
```

State effects:

| State | Tray icon |
|-------|-----------|
| `normal` | `app.ico` |
| `red` | `app-alert.ico` |

`TrayService.UpdateStatus(onlineCount, totalCount, isAlertActive)` applies the state computed by `Program.cs`.

## Toast Notifications vs Sounds

Toast notifications can still be shown for individual server offline/recovery status changes.

Sound timing is intentionally separate:

- Offline Toast notifications should not independently play the offline sound when `Program.cs` is already synchronizing sound with the red icon transition.
- The offline alert sound is skipped when `MonitoringSettings.OfflineNotificationSoundEnabled == false`.
- Recovery Toast notifications may still appear for individual servers, but the tray red -> normal transition does not play a sound.

## User Interactions That Can Change Tray State

These interactions can change the tray state and must refresh it:

- Add server
- Remove server
- Enable or disable server
- Edit server name or host
- Change failure threshold
- Change ping interval, because it changes how quickly probes occur in wall-clock time
- Pause or resume monitoring, if future behavior changes availability handling for paused servers
- Ping result arrives and changes consecutive failures or last successful ping time
- Ping result changes `Server.Status`

## Implementation Notes

- `MinuteRingBuffer` powers the GUI recent-minute chart and is not used by this tray state machine.
- Recovery is based on wall-clock availability: every enabled server must have at least one successful ping within the last 1 minute.
- `PingService.UpdateServers(...)` intentionally preserves runtime status for existing servers, so server-list changes must raise `ServersChanged`.
- Recovery can happen through successful probes, server deletion, disabling an unavailable server, editing a host so it succeeds, or removing all enabled servers.
- Alert can happen through failed probes, enabling an already failing server, or lowering the failure threshold below an existing consecutive failure streak.
