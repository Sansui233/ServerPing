<p align="center">
  <img src="ServerPing.GUI/Assets/App.Ico/Icon-tray_128x128.ico" alt="Server Ping logo" width="96" height="96">
  <h1 align="center">Server Ping</h1>
  <p align="center">一个轻量的 Windows 服务器在线状态监控应用，常驻托盘并及时提醒。</p>
</p>

<p align="center">
  <a href="README.md">English</a>
  ·
  <a href="README.zh-CN.md">简体中文</a>
</p>

<p align="center">
  <img src="docs/media/Screenshot.jpg" alt="Server ping 截图" width="860">
</p>

## 特点

- **低后台占用**：基于 WPF 构建，配合轻量常驻服务，适合长期在后台运行。
- **及时音效通知**：服务器离线时可开启音效提醒，及时获得反馈。
- **干净易用的现代界面**：用简洁的 Windows 原生界面管理服务器、设置、状态和可用率统计。
- **免配置**：可直接从 Windows Terminal 导入 SSH 连接配置。

## 系统要求

- **Windows**：Windows 10 1809 或更新版本。推荐使用 Windows 10 22H2 或 Windows 11。

## 下载

[Go to Releases](../../releases)

目前提供两种构建：

- **包含 .NET 的版本**：下载体积更大，无需额外安装 .NET 运行时。（推荐）
- **不包含 .NET 的版本**：体积更小，需要电脑上已安装 .NET 9 Desktop Runtime。[你可以在此下载 .Net9](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)。

两者运行效用、资源占用完全相同。

## License

Server ping 使用 **GPL-3.0** 协议开源。

## Thanks

- Icon by [Lucide](https://lucide.dev/).
