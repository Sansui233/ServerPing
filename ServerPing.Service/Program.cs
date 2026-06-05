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
var pingService = new PingService(statsFileManager);
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
        var settings = pingService.GetSettings();
        if (settings.OfflineNotificationEnabled)
        {
            notificationService.ShowServerOfflineNotification(e.Server, settings.FailureThreshold, playSound: false);
        }
    }
    else if (e.PreviousStatus == ServerStatus.Offline && e.Server.Status == ServerStatus.Online)
    {
        Console.WriteLine($"[恢复] {e.Server.Name} ({e.Server.Host})");
        notificationService.ShowServerOnlineNotification(e.Server);
    }

    RefreshTrayStatus(playTransitionSound: true);
};

pingService.SettingsChanged += (sender, e) => RefreshTrayStatus(playTransitionSound: true);
pingService.ServersChanged += (sender, e) => RefreshTrayStatus(playTransitionSound: true);
pingService.PingResultRecorded += (sender, e) => RefreshTrayStatus(playTransitionSound: true);

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
isTrayAlertActive = ShouldEnterAlertState(servers, pingService.GetSettings().FailureThreshold);
RefreshTrayStatus(playTransitionSound: false);

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

void RefreshTrayStatus(bool playTransitionSound)
{
    var servers = pingService.GetServers();
    var settings = pingService.GetSettings();
    var onlineCount = servers.Count(s => s.Status == ServerStatus.Online);
    var previousAlertState = isTrayAlertActive;
    isTrayAlertActive = GetNextTrayAlertState(servers, settings.FailureThreshold, isTrayAlertActive);

    if (playTransitionSound && previousAlertState != isTrayAlertActive)
    {
        if (isTrayAlertActive)
        {
            if (settings.OfflineNotificationSoundEnabled)
            {
                notificationService.PlayNotificationSound();
            }
        }
    }

    trayService.UpdateStatus(onlineCount, servers.Count, isTrayAlertActive);
}

bool GetNextTrayAlertState(IEnumerable<Server> servers, int threshold, bool currentAlertState) =>
    currentAlertState
        ? !ShouldLeaveAlertState(servers)
        : ShouldEnterAlertState(servers, threshold);

static bool ShouldEnterAlertState(IEnumerable<Server> servers, int threshold) =>
    servers.Any(s => s.IsEnabled && s.ConsecutiveFailures >= threshold);

bool ShouldLeaveAlertState(IEnumerable<Server> servers)
{
    var monitoredServers = servers.Where(s => s.IsEnabled).ToList();
    return monitoredServers.Count == 0
        || monitoredServers.All(s => pingService.WasAvailableInLastMinute(s.Id));
}
