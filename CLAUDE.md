# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ServerPing is a lightweight Windows server monitoring tool that:
- Pings servers every 5 seconds
- Sends Windows notifications after 3 consecutive failures
- Provides GUI for managing server list
- Imports SSH profiles from Windows Terminal configuration
- Runs as a background service with minimal resource footprint

**Technology Stack:** .NET 9, C#

## Architecture

Two-process design for minimal resource usage:

**ServerPing.Service** (Background Service)
- Console application that runs continuously in the background
- Executes ping operations on 5-second intervals
- Tracks failure counts and triggers Windows notifications
- Manages system tray icon and context menu
- Stores configuration in JSON format
- Target memory usage: 5-10MB when GUI is closed

**ServerPing.GUI** (Management Interface)
- WPF or Avalonia UI application
- Launched on-demand from system tray or standalone
- Edits server list (IP, Name, enabled/disabled state)
- Imports SSH profiles from `%LOCALAPPDATA%\Packages\Microsoft.WindowsTerminal_*\LocalState\settings.json`
- Communicates with Service via IPC (Named Pipes or gRPC)
- Completely exits when closed (no background GUI process)

## Configuration

Server list stored in JSON format, likely at:
- `%APPDATA%\ServerPing\servers.json` or
- Adjacent to Service executable

Windows Terminal settings location:
- `%LOCALAPPDATA%\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json`

## Development Commands

```bash
# Build entire solution
dotnet build

# Run Service (console mode for development)
dotnet run --project ServerPing.Service

# Run GUI
dotnet run --project ServerPing.GUI

# Run tests
dotnet test

# Publish self-contained executables
dotnet publish ServerPing.Service -c Release -r win-x64 --self-contained
dotnet publish ServerPing.GUI -c Release -r win-x64 --self-contained
```

## Project Constraints

- **Target Framework:** .NET 9
- **Platform:** Windows-only (uses Windows notifications and system tray APIs)
- **Resource Usage:** Service must stay under 20MB memory when GUI is closed
- **Process Isolation:** GUI must fully exit when closed; system tray remains via Service

## Implementation Notes

- Use `System.Net.NetworkInformation.Ping` for ICMP ping operations
- Windows notifications via `Microsoft.Toolkit.Uwp.Notifications` or native Win32 APIs
- System tray via `System.Windows.Forms.NotifyIcon` (minimal dependency)
- IPC between Service and GUI to avoid tight coupling
- Windows Terminal profile parsing: extract `name` and `commandline` from profiles with SSH connections
