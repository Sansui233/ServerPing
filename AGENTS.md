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

```
ServerPing.sln
├── ServerPing.Shared    (Class Library, net9.0)      — 共享模型和 IPC 协议
├── ServerPing.Service   (Console App, net9.0-windows) — 后台服务
└── ServerPing.GUI       (WPF App, net9.0-windows)     — 管理面板
```

**ServerPing.Shared**
- `Models/Server.cs` — 服务器模型：Id, Name, Host, IsEnabled, Status, LastPingTime, ConsecutiveFailures
- `Models/ServerStatus.cs` — 枚举：Online, Offline, Unknown
- `Models/ServerConfiguration.cs` — 配置根对象，包含 `List<Server>`
- `ConfigurationManager.cs` — 读写 `%APPDATA%\ServerPing\servers.json`
- `IPC/MessageType.cs` — IPC 命令枚举：GetServers, UpdateServers, AddServer, RemoveServer, GetStatus
- `IPC/IpcMessages.cs` — IPC 消息/响应类型、AddServerRequest、RemoveServerRequest

**ServerPing.Service** (后台常驻)
- `Program.cs` — 主入口，整合所有组件，使用 `Application.Run()` 驱动消息循环
- `PingService.cs` — Ping 引擎，每服务器独立 `System.Threading.Timer`（5秒间隔），连续3次失败触发状态变更事件
- `NotificationService.cs` — Windows Toast 通知（使用 `ToastNotificationManager` API）
- `TrayService.cs` — 系统托盘图标和右键菜单（`System.Windows.Forms.NotifyIcon`）
- `IpcServer.cs` — Named Pipe 服务端（`\\.\pipe\ServerPing`），处理 GUI 发来的命令
- `GuiProcessManager.cs` — 启动/检测 GUI.exe 进程

**ServerPing.GUI** (按需启动，关闭后完全退出)
- `App.xaml.cs` — 单实例保护（Mutex）
- `MainWindow.xaml` — 主界面：服务器列表 DataGrid + 添加面板 + 状态栏
- `ViewModels/MainViewModel.cs` — MVVM 主 ViewModel，3秒轮询 IPC 刷新状态
- `ViewModels/ServerViewModel.cs` — 服务器行 ViewModel
- `Services/IpcClient.cs` — Named Pipe 客户端
- `Services/WindowsTerminalParser.cs` — 解析 Windows Terminal settings.json，提取 SSH Profile
- `ImportDialog.xaml` — SSH Profile 导入选择对话框
- `Converters/EnableToggleConverter.cs` — 启用/禁用按钮文本转换

## IPC Protocol

通过 Named Pipe `ServerPing` 通信，JSON 格式消息：
- **请求:** `IpcMessage { Type: MessageType, Data: object? }`
- **响应:** `IpcResponse { Success: bool, ErrorMessage?: string, Data?: object }`
- 单连接模式：每次请求建立新连接，完成后断开

## Configuration

- 配置文件位置：`%APPDATA%\ServerPing\servers.json`
- 格式：`ServerConfiguration { Servers: List<Server> }`
- 由 Service 端读写，GUI 通过 IPC 间接修改

Windows Terminal settings 位置：
- `%LOCALAPPDATA%\Packages\Microsoft.WindowsTerminal_*/LocalState\settings.json`

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

## Current Status (Phase 1-5 Complete)

已完成的核心功能：
- ✅ 项目结构和共享模型
- ✅ Ping 引擎（5秒间隔，连续3次失败触发通知）
- ✅ Windows Toast 通知（离线/恢复）
- ✅ 系统托盘图标和右键菜单
- ✅ Named Pipe IPC（Service ↔ GUI）
- ✅ WPF 管理面板（MVVM，添加/删除/启用禁用/实时状态）
- ✅ Windows Terminal SSH Profile 导入
- ✅ GUI 单实例保护（Mutex）

## Pending Work (Phase 6-7)

后续待实现功能：
- 托盘右键菜单显示各服务器状态
- Ping 间隔可配置化（当前硬编码 5 秒）
- 失败通知阈值可配置化（当前硬编码连续 3 次）
- 托盘图标状态指示（全在线 vs 有离线用不同图标/颜色）
- 暂停监控功能实际接线（当前 MonitoringToggleRequested 仅打印日志）
- 自包含发布和打包
- 开机自启动支持
- 长时间运行稳定性测试

## Project Constraints

- **Target Framework:** .NET 9
- **Platform:** Windows-only (uses Windows notifications and system tray APIs)
- **Resource Usage:** Service must stay under 20MB memory when GUI is closed
- **Process Isolation:** GUI must fully exit when closed; system tray remains via Service
