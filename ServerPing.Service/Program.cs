using System.Windows.Forms;
using Microsoft.Win32;
using ServerPing.Service;
using ServerPing.Shared;
using ServerPing.Shared.Models;

Application.SetHighDpiMode(HighDpiMode.SystemAware);
Application.EnableVisualStyles();

Console.WriteLine("ServerPing Service 启动中...");

var config = ConfigurationManager.Load();
Console.WriteLine($"已加载 {config.Servers.Count} 个服务器配置");

var statsFileManager = new StatsFileManager();
var localNetworkMonitor = new LocalNetworkMonitor();
var pingService = new PingService(statsFileManager, localNetworkMonitor);
var notificationService = new NotificationService();
var startupRegistrationService = new StartupRegistrationService();
startupRegistrationService.Apply(config.Settings.LaunchAtStartup);
var ipcServer = new IpcServer(pingService, notificationService, startupRegistrationService);
var trayService = new TrayService(
    () => pingService.GetServers(),
    serverId => pingService.GetLastHourAvailability(serverId),
    () => pingService.GetSettings());
var guiManager = new GuiProcessManager();
var isTrayAlertActive = false;

pingService.StatusChanged += (sender, e) =>
{
    if (e.PreviousStatus != ServerStatus.Offline && e.Server.Status == ServerStatus.Offline)
    {
        Console.WriteLine($"[离线] {e.Server.Name} ({e.Server.Host})");
        notificationService.ShowServerOfflineNotification(e.Server, pingService.GetSettings().FailureThreshold, playSound: false);

        if (pingService.GetSettings().OfflineNotificationSoundEnabled)
        {
            notificationService.PlayNotificationSound();
        }
    }
    else if (e.PreviousStatus == ServerStatus.Offline && e.Server.Status == ServerStatus.Online)
    {
        Console.WriteLine($"[恢复] {e.Server.Name} ({e.Server.Host})");
        notificationService.ShowServerOnlineNotification(e.Server);
    }

    RefreshTrayStatus();
};

pingService.SettingsChanged += (sender, e) => RefreshTrayStatus();
pingService.ServersChanged += (sender, e) => RefreshTrayStatus();
pingService.PingResultRecorded += (sender, e) => RefreshTrayStatus();
localNetworkMonitor.StatusChanged += (sender, e) =>
{
    Console.WriteLine($"本地网络状态: {pingService.GetLocalNetworkStatus()}");
    RefreshTrayStatus();
};

trayService.OpenGuiRequested += (sender, e) => guiManager.LaunchGui();
trayService.ToggleGuiRequested += async (s, pos) => await guiManager.ToggleGui(pos.X, pos.Y);
trayService.MonitoringToggleRequested += (sender, paused) =>
{
    if (paused)
    {
        pingService.Pause();
        Console.WriteLine("监控已暂停");
    }
    else
    {
        pingService.Resume();
        Console.WriteLine("监控已恢复");
    }
};

trayService.LaunchAtStartupToggleRequested += (sender, enabled) =>
{
    var currentConfig = ConfigurationManager.Load();
    currentConfig.Settings.LaunchAtStartup = enabled;
    startupRegistrationService.Apply(enabled);
    ConfigurationManager.Save(currentConfig);
    pingService.UpdateSettings(currentConfig.Settings);
};

trayService.ExitRequested += (sender, e) =>
{
    guiManager.CloseGuiIfRunning();
    Application.Exit();
};

pingService.Start(config.Servers, config.Settings);
ipcServer.Start();

var servers = pingService.GetServers();
isTrayAlertActive = ShouldShowAlertState(servers);
RefreshTrayStatus();

Console.WriteLine("监控服务已启动，系统托盘图标已显示");

SystemEvents.SessionEnding += (s, e) => { statsFileManager.FlushAll(); };

if (!config.Settings.SilentStartup)
{
    guiManager.LaunchGui();
}

Application.Run();

Console.WriteLine("\n正在关闭服务...");
trayService.Dispose();
ipcServer.Dispose();
pingService.Dispose();
Console.WriteLine("服务已停止");

void RefreshTrayStatus()
{
    var servers = pingService.GetServers();
    var onlineCount = servers.Count(s => s.Status == ServerStatus.Online);
    isTrayAlertActive = ShouldShowAlertState(servers);

    trayService.UpdateStatus(onlineCount, servers.Count, isTrayAlertActive);
}

static bool ShouldShowAlertState(IEnumerable<Server> servers) =>
    servers.Any(s => s.IsEnabled && s.Status == ServerStatus.Offline);
