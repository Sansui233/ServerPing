using System.Diagnostics;
using System.Windows.Forms;

namespace ServerPing.Service;

public class TrayService : IDisposable
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private bool _isMonitoringPaused;

    public event EventHandler? OpenGuiRequested;
    public event EventHandler<bool>? MonitoringToggleRequested;
    public event EventHandler? ExitRequested;

    public TrayService()
    {
        _contextMenu = new ContextMenuStrip();

        var openGuiItem = new ToolStripMenuItem("打开管理面板");
        openGuiItem.Click += (s, e) => OpenGuiRequested?.Invoke(this, EventArgs.Empty);

        var toggleMonitoringItem = new ToolStripMenuItem("暂停监控");
        toggleMonitoringItem.Click += (s, e) =>
        {
            _isMonitoringPaused = !_isMonitoringPaused;
            toggleMonitoringItem.Text = _isMonitoringPaused ? "恢复监控" : "暂停监控";
            MonitoringToggleRequested?.Invoke(this, _isMonitoringPaused);
        };

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Items.Add(openGuiItem);
        _contextMenu.Items.Add(toggleMonitoringItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Text = "ServerPing",
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _trayIcon.DoubleClick += (s, e) => OpenGuiRequested?.Invoke(this, EventArgs.Empty);

        LoadIcon();
    }

    private void LoadIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                _trayIcon.Icon = new Icon(iconPath);
            }
            else
            {
                _trayIcon.Icon = SystemIcons.Application;
            }
        }
        catch
        {
            _trayIcon.Icon = SystemIcons.Application;
        }
    }

    public void UpdateStatus(int onlineCount, int totalCount)
    {
        _trayIcon.Text = $"ServerPing - {onlineCount}/{totalCount} 在线";
    }

    public void Dispose()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _contextMenu.Dispose();
    }
}
