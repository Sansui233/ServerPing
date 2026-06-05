# Tray and Offline State Machines

This document tracks two related but separate concepts:

- Per-server `Server.Status`, especially when a server enters or leaves `Offline`.
- Tray alert state, which is an aggregate view of all enabled servers.

Availability statistics are intentionally separate. Recent-minute availability is a reporting metric; it does not decide whether a server is `Offline` and does not decide whether the tray shows the alert icon.

## Responsibilities

| Module | Responsibility |
|--------|----------------|
| `PingService` | Owns per-server ping state, consecutive failure counts, rolling availability statistics, and `StatusChanged` events. |
| `Program.cs` | Reacts to server status changes, shows Toast notifications, plays per-server offline sounds, and computes tray alert state. |
| `TrayService` | Applies the tray icon, tooltip, and menu rendering from the state passed by `Program.cs`. |
| `NotificationService` | Shows Toast notifications and plays the bundled offline sound or fallback system sound. |

## Per-Server Offline State

Each enabled server is evaluated independently.

```text
                         successful ping
        +----------------------------------------------+
        |                                              v
   +---------+                                    +---------+
   | Unknown |                                    | Online  |
   +---------+                                    +---------+
        |                                              |
        | failed pings keep Unknown                    | failed pings keep Online
        | until threshold is reached                   | until threshold is reached
        |                                              |
        | ConsecutiveFailures >= FailureThreshold      | ConsecutiveFailures >= FailureThreshold
        v                                              v
   +---------+ <--------------------------------- +---------+
   | Offline |      failed pings keep Offline     | Online  |
   +---------+                                    +---------+
        |
        | successful ping resets ConsecutiveFailures to 0
        v
   +---------+
   | Online  |
   +---------+
```

Rules:

- A failed ping increments `Server.ConsecutiveFailures`.
- A successful ping sets `Server.Status = Online` and resets `Server.ConsecutiveFailures = 0`.
- When `Server.ConsecutiveFailures >= MonitoringSettings.FailureThreshold`, `PingService` sets `Server.Status = Offline`.
- Once a server is `Offline`, additional failed pings keep it `Offline`; they do not create another `StatusChanged` event.
- A server leaves `Offline` only on a successful ping.

## Status Change Effects

`Program.cs` handles `PingService.StatusChanged`.

```text
Server status changed
        |
        +-- previous != Offline && current == Offline
        |       |
        |       +-- show offline Toast
        |       +-- play offline sound if OfflineNotificationSoundEnabled
        |       +-- refresh tray state
        |
        +-- previous == Offline && current == Online
        |       |
        |       +-- show recovery Toast
        |       +-- refresh tray state
        |
        +-- any other status change
                |
                +-- refresh tray state
```

Important behavior:

- Offline sound is tied to an individual server entering `Offline`, not to the tray icon transition.
- If three servers independently enter `Offline`, the offline sound can play three times.
- Offline Toasts are created with `playSound: false`; the explicit offline sound is played by `Program.cs`.
- Recovery does not play the offline sound.

## Tray Alert State

The tray state is an aggregate of all enabled servers.

```text
                         any enabled server is Offline
       +--------+ --------------------------------------------> +-------+
       | Normal |                                               | Alert |
       +--------+ <-------------------------------------------- +-------+
                    no enabled server is Offline
```

State effects:

| State | Condition | Tray icon |
|-------|-----------|-----------|
| `Normal` | No enabled server has `Status == Offline` | `app.ico` |
| `Alert` | At least one enabled server has `Status == Offline` | `app-alert.ico` |

`Program.cs` computes this with:

```csharp
servers.Any(s => s.IsEnabled && s.Status == ServerStatus.Offline)
```

The tray icon can enter `Alert` when a server enters `Offline`, or when a server that is already `Offline` becomes enabled. The tray icon can leave `Alert` when all enabled servers leave `Offline`, are disabled, or are removed.

## Events That Refresh Tray State

| Event | Why it matters |
|-------|----------------|
| `PingService.StatusChanged` | A server may have entered or left `Offline`. This is also where per-server Toast and offline sound behavior runs. |
| `PingService.PingResultRecorded` | Keeps tray tooltip counts and availability display current. Usually does not change alert state unless status also changed. |
| `PingService.ServersChanged` | Add, delete, enable, disable, or edit operations can change whether an enabled `Offline` server exists. |
| `PingService.SettingsChanged` | Threshold or interval changes affect future ping/status behavior and should refresh tray display. |

Startup refreshes tray state without playing sounds. Existing offline servers may make the tray start in `Alert`, but startup does not replay offline sounds.

## Availability

Availability is a statistics concept, not an offline-state concept.

- `MinuteRingBuffer` powers recent-minute GUI charts.
- `StatsFileManager` powers longer rolling availability stats.
- Ping interval settings change how many samples are recorded over time.
- Availability can remain high immediately after a server enters `Offline` because it summarizes a time window.
- Tray alert state must not use recent availability; it uses `Server.Status == Offline`.
