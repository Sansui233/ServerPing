# Startup Registration

ServerPing's "Launch at startup" option is intentionally opt-in. The default is off.

## User setting

The persisted source of truth is:

- `ServerPing.Shared.Models.MonitoringSettings.LaunchAtStartup`
- Stored in `%APPDATA%\ServerPing\servers.json` as part of `ServerConfiguration.Settings`

The GUI settings dialog and the Service tray menu both update this same setting through the Service-side configuration flow. The GUI must not write `servers.json` or startup registration directly.

## Windows registration

When `LaunchAtStartup` is enabled, the Service writes a per-user Run entry:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
ServerPing = "C:\path\to\currently\running\ServerPing.exe"
```

The executable path comes from `Application.ExecutablePath`, so it is based on the currently running Service binary. For example, a Debug run may register a path under `ServerPing.Service\bin\Debug\...`, while a published build registers the published `ServerPing.exe` path.

No administrator permission is required because the entry is under `HKCU`.

## Synchronization points

Startup registration is synchronized in these places:

- Service startup: `Program.cs` calls `StartupRegistrationService.Apply(config.Settings.LaunchAtStartup)`.
- GUI settings save: `IpcServer.UpdateSettings` applies `LaunchAtStartup`, saves config, then updates `PingService`.
- Tray menu toggle: `TrayService.LaunchAtStartupToggleRequested` updates config, applies the registry entry, saves config, then updates `PingService`.

Because the Service applies the setting at startup, if the app is moved or run from a new build path while `LaunchAtStartup` is true, launching the Service once from the new location refreshes the registry value to the new executable path.

## Tray behavior

The tray menu item text is localized by `SharedLocalization` using:

```text
Tray.LaunchAtStartup
```

The menu item is checked when `PingService.GetSettings().LaunchAtStartup` is true.

## Troubleshooting

If startup does not work:

1. Check `%APPDATA%\ServerPing\servers.json` and confirm `LaunchAtStartup` is `true`.
2. Check the Run key value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
3. Confirm the registered executable path exists.
4. If the app was moved, start the Service once from the new location to refresh the Run entry.

