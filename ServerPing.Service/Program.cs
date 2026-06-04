using System.Windows.Forms;
using ServerPing.Service;
using ServerPing.Shared;
using ServerPing.Shared.Models;

Application.SetHighDpiMode(HighDpiMode.SystemAware);
Application.EnableVisualStyles();

Console.WriteLine("ServerPing Service 启动中...");

var config = ConfigurationManager.Load();
Console.WriteLine($"已加载 {config.Servers.Count} 个服务器配置");

var pingService = new PingService();
var notificationService = new NotificationService();
var ipcServer = new IpcServer(pingService);
var trayService = new TrayService();
var guiManager = new GuiProcessManager();

pingService.StatusChanged += (sender, e) =>
{
    if (e.PreviousStatus != ServerStatus.Offline && e.Server.Status == ServerStatus.Offline)
    {
        Console.WriteLine($"[离线] {e.Server.Name} ({e.Server.Host})");
        notificationService.ShowServerOfflineNotification(e.Server);
    }
    else if (e.PreviousStatus == ServerStatus.Offline && e.Server.Status == ServerStatus.Online)
    {
        Console.WriteLine($"[恢复] {e.Server.Name} ({e.Server.Host})");
        notificationService.ShowServerOnlineNotification(e.Server);
    }

    var servers = pingService.GetServers();
    var onlineCount = servers.Count(s => s.Status == ServerStatus.Online);
    trayService.UpdateStatus(onlineCount, servers.Count);
};

trayService.OpenGuiRequested += (sender, e) => guiManager.LaunchGui();
trayService.MonitoringToggleRequested += (sender, paused) =>
{
    Console.WriteLine(paused ? "监控已暂停" : "监控已恢复");
};

trayService.ExitRequested += (sender, e) => Application.Exit();

pingService.Start(config.Servers);
ipcServer.Start();

var servers = pingService.GetServers();
var onlineCount = servers.Count(s => s.Status == ServerStatus.Online);
trayService.UpdateStatus(onlineCount, servers.Count);

Console.WriteLine("监控服务已启动，系统托盘图标已显示");

Application.Run();

Console.WriteLine("\n正在关闭服务...");
trayService.Dispose();
ipcServer.Dispose();
pingService.Dispose();
Console.WriteLine("服务已停止");
