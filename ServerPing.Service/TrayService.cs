using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ServerPing.Shared.Localization;
using ServerPing.Shared.Models;

namespace ServerPing.Service;

public class TrayService : IDisposable
{
    private const string DefaultIconFileName = "app.ico";
    private const string AlertIconFileName = "app-alert.ico";

    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly Form _menuOwner;
    private readonly ToolStripSeparator _statusSeparator;
    private readonly ToolStripMenuItem _openGuiItem;
    private readonly ToolStripMenuItem _launchAtStartupItem;
    private readonly ToolStripMenuItem _toggleMonitoringItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly Func<List<Server>> _getServers;
    private readonly Func<string, double?> _getLastHourAvailability;
    private readonly Func<MonitoringSettings> _getSettings;
    private readonly List<ToolStripItem> _statusItems = [];
    private Icon? _defaultIcon;
    private Icon? _alertIcon;
    private bool _isAlertIconActive;
    private bool _isMonitoringPaused;
    private bool _disposed;

    public event EventHandler? OpenGuiRequested;
    public event EventHandler<(int X, int Y)>? ToggleGuiRequested;
    public event EventHandler<bool>? MonitoringToggleRequested;
    public event EventHandler<bool>? LaunchAtStartupToggleRequested;
    public event EventHandler? ExitRequested;

    public TrayService(
        Func<List<Server>> getServers,
        Func<string, double?> getLastHourAvailability,
        Func<MonitoringSettings> getSettings)
    {
        _getServers = getServers;
        _getLastHourAvailability = getLastHourAvailability;
        _getSettings = getSettings;
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

        _openGuiItem = new ToolStripMenuItem();
        _openGuiItem.Click += (s, e) => OpenGuiRequested?.Invoke(this, EventArgs.Empty);

        _statusSeparator = new ToolStripSeparator();

        _launchAtStartupItem = new ToolStripMenuItem();
        _launchAtStartupItem.Click += (s, e) =>
        {
            var enabled = !_getSettings().LaunchAtStartup;
            LaunchAtStartupToggleRequested?.Invoke(this, enabled);
            ApplyLanguage();
        };

        _toggleMonitoringItem = new ToolStripMenuItem();
        _toggleMonitoringItem.Click += (s, e) =>
        {
            _isMonitoringPaused = !_isMonitoringPaused;
            ApplyLanguage();
            MonitoringToggleRequested?.Invoke(this, _isMonitoringPaused);
        };

        _exitItem = new ToolStripMenuItem();
        _exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _contextMenu.Opening += (s, e) =>
        {
            RefreshStatusItems();
        };

        ApplyLanguage();
        BuildMenuShell();

        _trayIcon = new NotifyIcon
        {
            Text = "ServerPing",
            Visible = true
        };

        _trayIcon.MouseClick += TrayIcon_MouseClick;

        LoadIcons();
        ApplyTrayIcon(hasOfflineServers: false);
    }

    private void TrayIcon_MouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var pos = Cursor.Position;
            ToggleGuiRequested?.Invoke(this, (pos.X, pos.Y));
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
        if (_disposed)
        {
            return;
        }

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
        _contextMenu.Items.Add(_launchAtStartupItem);
        _contextMenu.Items.Add(_toggleMonitoringItem);
        _contextMenu.Items.Add(_exitItem);
    }

    private void RefreshStatusItems()
    {
        if (_disposed)
        {
            return;
        }

        ApplyLanguage();

        foreach (var item in _statusItems)
        {
            item.Dispose();
        }

        _statusItems.Clear();
        var servers = _getServers();

        if (servers.Count == 0)
        {
            _statusItems.Add(CreateStatusItem(T("Tray.NoServers")));
        }
        else
        {
            foreach (var server in servers)
            {
                var availability = _getLastHourAvailability(server.Id);
                _statusItems.Add(CreateStatusItem(FormatServerStatus(server, availability, CurrentLanguage())));
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing)
        {
            _trayIcon.MouseClick -= TrayIcon_MouseClick;
            _trayIcon.Visible = false;
            _trayIcon.Icon = null;
            _trayIcon.Dispose();

            foreach (var item in _statusItems)
            {
                item.Dispose();
            }

            _statusItems.Clear();

            if (_alertIcon is not null && !ReferenceEquals(_alertIcon, _defaultIcon))
            {
                _alertIcon.Dispose();
            }

            _defaultIcon?.Dispose();

            _contextMenu.Dispose();
            _menuOwner.Dispose();
        }
    }

    private void LoadIcons()
    {
        _defaultIcon = LoadIcon(DefaultIconFileName) ?? CreateFallbackIcon();
        _alertIcon = LoadIcon(AlertIconFileName) ?? _defaultIcon;
    }

    private static Icon CreateFallbackIcon() => (Icon)SystemIcons.Application.Clone();

    private static Icon? LoadIcon(string fileName)
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            return File.Exists(iconPath) ? new Icon(iconPath) : null;
        }
        catch
        {
            return null;
        }
    }

    private void ApplyTrayIcon(bool hasOfflineServers)
    {
        if (_disposed)
        {
            return;
        }

        if (_isAlertIconActive == hasOfflineServers && _trayIcon.Icon is not null)
        {
            return;
        }

        _trayIcon.Icon = hasOfflineServers ? _alertIcon : _defaultIcon;
        _isAlertIconActive = hasOfflineServers;
    }

    public void UpdateStatus(int onlineCount, int totalCount, bool isAlertActive)
    {
        if (_disposed)
        {
            return;
        }

        var servers = _getServers();
        ApplyTrayIcon(isAlertActive);

        var availability = AverageAvailability(servers
            .Select(server => _getLastHourAvailability(server.Id)));
        var text = string.Format(
            T("Tray.Tooltip"),
            onlineCount,
            totalCount,
            FormatAvailability(availability));
        _trayIcon.Text = text.Length <= 63 ? text : text[..63];
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private string CurrentLanguage() => _getSettings().Language;

    private string T(string key) => SharedLocalization.Get(CurrentLanguage(), key);

    private void ApplyLanguage()
    {
        _openGuiItem.Text = T("Tray.OpenGui");
        _launchAtStartupItem.Text = T("Tray.LaunchAtStartup");
        _launchAtStartupItem.Checked = _getSettings().LaunchAtStartup;
        _toggleMonitoringItem.Text = _isMonitoringPaused
            ? T("Tray.ResumeMonitoring")
            : T("Tray.PauseMonitoring");
        _exitItem.Text = T("Tray.Exit");
    }

    private static string FormatServerStatus(Server server, double? availability, string language) =>
        $"{SharedLocalization.StatusText(language, server.Status)}  {server.Name} ({server.Host})  {SharedLocalization.Get(language, "Tray.Availability")}: {FormatAvailability(availability)}";

    private static string FormatAvailability(double? availability) =>
        availability.HasValue ? $"{availability.Value:0.#}%" : "--";

    private static double? AverageAvailability(IEnumerable<double?> values)
    {
        var availableValues = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return availableValues.Count == 0 ? null : availableValues.Average();
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
