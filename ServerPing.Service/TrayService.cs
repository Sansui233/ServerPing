using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ServerPing.Shared.Models;

namespace ServerPing.Service;

public class TrayService : IDisposable
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly Form _menuOwner;
    private readonly ToolStripSeparator _statusSeparator;
    private readonly ToolStripMenuItem _openGuiItem;
    private readonly ToolStripMenuItem _toggleMonitoringItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly Func<List<Server>> _getServers;
    private readonly Func<string, double?> _getLastHourAvailability;
    private readonly List<ToolStripItem> _statusItems = [];
    private bool _isMonitoringPaused;

    public event EventHandler? OpenGuiRequested;
    public event EventHandler<bool>? MonitoringToggleRequested;
    public event EventHandler? ExitRequested;

    public TrayService(Func<List<Server>> getServers, Func<string, double?> getLastHourAvailability)
    {
        _getServers = getServers;
        _getLastHourAvailability = getLastHourAvailability;
        _contextMenu = new ContextMenuStrip();
        _menuOwner = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            Size = new Size(1, 1),
            StartPosition = FormStartPosition.Manual,
            Opacity = 0
        };
        _menuOwner.Show();
        _menuOwner.Hide();
        _contextMenu.Closed += (s, e) => _menuOwner.Hide();

        _openGuiItem = new ToolStripMenuItem("打开管理面板");
        _openGuiItem.Click += (s, e) => OpenGuiRequested?.Invoke(this, EventArgs.Empty);

        _statusSeparator = new ToolStripSeparator();

        _toggleMonitoringItem = new ToolStripMenuItem("暂停监控");
        _toggleMonitoringItem.Click += (s, e) =>
        {
            _isMonitoringPaused = !_isMonitoringPaused;
            _toggleMonitoringItem.Text = _isMonitoringPaused ? "恢复监控" : "暂停监控";
            MonitoringToggleRequested?.Invoke(this, _isMonitoringPaused);
        };

        _exitItem = new ToolStripMenuItem("退出");
        _exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Opening += (s, e) =>
        {
            RefreshStatusItems();
        };

        BuildMenuShell();

        _trayIcon = new NotifyIcon
        {
            Text = "ServerPing",
            Visible = true
        };

        _trayIcon.MouseClick += TrayIcon_MouseClick;

        LoadIcon();
    }

    private void TrayIcon_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            OpenGuiRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            RefreshStatusItems();
            ShowContextMenu();
        }
    }

    private void ShowContextMenu()
    {
        _menuOwner.Location = Cursor.Position;
        _menuOwner.Show();
        SetForegroundWindow(_menuOwner.Handle);
        _contextMenu.Show(_menuOwner, Point.Empty, ToolStripDropDownDirection.AboveRight);
    }

    private void BuildMenuShell()
    {
        _contextMenu.Items.Clear();
        _contextMenu.Items.Add(_openGuiItem);
        _contextMenu.Items.Add(_statusSeparator);
        _contextMenu.Items.AddRange(_statusItems.ToArray());
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_toggleMonitoringItem);
        _contextMenu.Items.Add(_exitItem);
    }

    private void RefreshStatusItems()
    {
        foreach (var item in _statusItems)
        {
            item.Dispose();
        }

        _statusItems.Clear();
        var servers = _getServers();

        if (servers.Count == 0)
        {
            _statusItems.Add(CreateStatusItem("暂无服务器"));
        }
        else
        {
            foreach (var server in servers)
            {
                var availability = _getLastHourAvailability(server.Id);
                _statusItems.Add(CreateStatusItem(FormatServerStatus(server, availability)));
            }
        }

        BuildMenuShell();
    }

    private static ToolStripMenuItem CreateStatusItem(string text) => new(text)
    {
        Enabled = false
    };

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var item in _statusItems)
            {
                item.Dispose();
            }
        }
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
        Dispose(true);
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _contextMenu.Dispose();
        _menuOwner.Dispose();
    }

    private static string FormatServerStatus(Server server, double? availability) =>
        $"{StatusText(server.Status)}  {server.Name} ({server.Host})  {FormatAvailability(availability)}";

    private static string StatusText(ServerStatus status) => status switch
    {
        ServerStatus.Online => "在线",
        ServerStatus.Offline => "离线",
        _ => "未知"
    };

    private static string FormatAvailability(double? availability) =>
        availability.HasValue ? $"{availability.Value:0.#}%" : "--";

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
