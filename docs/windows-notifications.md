# Windows Notifications

## Research Summary

ServerPing uses the resident Service process to send Windows Toast notifications. The WPF GUI does not call Windows notification APIs directly; it sends IPC commands to the Service so monitoring, tray state, and notifications stay in one process.

Official Windows App SDK documentation shows modern desktop notifications through `AppNotificationManager.Default.Show(...)`. For unpackaged desktop apps, Windows App SDK APIs require bootstrap initialization, and the Windows App SDK runtime packages must be installed on the device.

ServerPing intentionally keeps the lighter existing implementation:

- `Microsoft.Toolkit.Uwp.Notifications` builds Toast XML with `ToastContentBuilder`.
- `Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier("ServerPing").Show(...)` sends the system Toast.
- `winmm.dll` `PlaySound` plays the bundled offline alert sound from the Service output directory.

This avoids adding a Windows App SDK runtime dependency for the current unpackaged publish model.

## WPF Notification Flow

The GUI triggers notification actions through IPC:

- `SettingsDialog` calls `MainViewModel.TestNotificationAsync()`.
- `MainViewModel` calls `IpcClient.TestNotificationAsync()`.
- `IpcClient` sends `MessageType.TestNotification` to `\\.\pipe\ServerPing`.
- `IpcServer` calls `NotificationService.ShowTestNotification()`.
- `NotificationService` builds and shows the Windows Toast.

Runtime monitoring notifications follow the same Service ownership:

- `PingService` raises `StatusChanged`.
- `Program.cs` handles offline/recovery transitions.
- If `OfflineNotificationEnabled` is true, `NotificationService` sends the offline Toast.
- Recovery Toasts are still sent when an offline server returns online.

## Custom Sound

Windows Toast audio is limited by platform rules and app identity. Packaged apps can use app package resources, and built-in notification sounds are available through platform sound URIs. For this unpackaged tool, ServerPing treats the custom alert sound as an app-controlled sound rather than Toast XML audio.

Implementation:

- `ServerPing.GUI/Assets/Sounds/Offline.wav` is linked into `ServerPing.Service` output as `offline.wav`.
- `NotificationService.PlayNotificationSound()` tries `offline.wav`.
- If the bundled file is missing or playback fails, it falls back to the Windows `SystemNotification` alias.
- If that also fails, it falls back to `SystemSounds.Exclamation`.

The `OfflineNotificationEnabled` setting controls whether offline Toast notifications are sent. The `OfflineNotificationSoundEnabled` setting separately controls whether the offline tray alert transition plays the custom sound.

## Build And Install Requirements

Project requirements:

- `ServerPing.Service` targets `net9.0-windows10.0.19041.0`.
- `ServerPing.Service` sets `TargetPlatformMinVersion` to `10.0.17763.0`.
- `ServerPing.Service` references `Microsoft.Toolkit.Uwp.Notifications` `7.1.3`.
- `ServerPing.GUI` targets `net9.0-windows` with WPF enabled.

Publish requirements:

- Service and GUI must be published into the same output directory.
- The Service executable is the entry point.
- The GUI executable must be next to the Service executable so `GuiProcessManager` can launch it.
- `offline.wav` must be present next to the Service executable; the Service project copies it during build and publish.

Runtime requirements:

- Windows 10 or later.
- For framework-dependent publish, the target machine needs the .NET 9 runtime.
- For self-contained publish, no separate .NET runtime install is required.
- No Windows App SDK runtime is required by the current Toolkit-based implementation.
- The Settings dialog's test notification uses the same Service IPC and Toast sender as the portable build. It can work in a portable/framework-dependent publish as long as the Service is running, both executables are in the same publish directory, the .NET 9 runtime is installed, and Windows notifications are not disabled by OS/user policy.
