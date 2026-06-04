using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ServerPing.GUI.Services;
using ServerPing.Shared.Models;

namespace ServerPing.GUI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IpcClient _ipcClient = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _undoTimer;
    private ServerViewModel? _selectedServer;
    private string _statusMessage = "正在连接服务...";
    private bool _isConnected;
    private bool _canUndo;
    private string? _lastDeletedName;
    private string? _lastDeletedHost;

    public ObservableCollection<ServerViewModel> Servers { get; } = [];

    public ServerViewModel? SelectedServer
    {
        get => _selectedServer;
        set => SetProperty(ref _selectedServer, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public bool CanUndo
    {
        get => _canUndo;
        set => SetProperty(ref _canUndo, value);
    }

    public RelayCommand AddServerCommand { get; }
    public RelayCommand RemoveServerCommand { get; }
    public RelayCommand ToggleServerCommand { get; }
    public RelayCommand ImportFromTerminalCommand { get; }
    public RelayCommand UndoDeleteCommand { get; }

    public MainViewModel()
    {
        AddServerCommand = new RelayCommand(async () => await AddNewRowAsync());
        RemoveServerCommand = new RelayCommand(async (p) => await RemoveServerAsync(p),
            (p) => p is ServerViewModel);
        ToggleServerCommand = new RelayCommand(async (p) => await ToggleServerAsync(p),
            (p) => p is ServerViewModel);
        ImportFromTerminalCommand = new RelayCommand(ImportFromTerminal);
        UndoDeleteCommand = new RelayCommand(async () => await UndoDeleteAsync(), () => CanUndo);

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += async (s, e) => await RefreshServersAsync();

        _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _undoTimer.Tick += (s, e) => ClearUndo();
    }

    public async Task InitializeAsync()
    {
        await RefreshServersAsync();
        _refreshTimer.Start();
    }

    public void Shutdown()
    {
        _refreshTimer.Stop();
        _undoTimer.Stop();
    }

    private async Task RefreshServersAsync()
    {
        try
        {
            var servers = await _ipcClient.GetServersAsync();

            foreach (var serverModel in servers)
            {
                var existing = Servers.FirstOrDefault(s => s.Id == serverModel.Id);
                if (existing != null)
                    existing.UpdateFrom(serverModel);
                else
                    Servers.Add(ServerViewModel.FromModel(serverModel));
            }

            var serverIds = servers.Select(s => s.Id).ToHashSet();
            var toRemove = Servers.Where(s => !serverIds.Contains(s.Id)).ToList();
            foreach (var s in toRemove)
                Servers.Remove(s);

            var onlineCount = servers.Count(s => s.Status == ServerStatus.Online);
            StatusMessage = $"已连接 — {onlineCount}/{servers.Count} 在线";
            IsConnected = true;

            await RefreshStatsAsync();
        }
        catch
        {
            StatusMessage = "无法连接到服务，请确认 ServerPing Service 正在运行";
            IsConnected = false;
        }
    }

    private async Task RefreshStatsAsync()
    {
        var stats = await _ipcClient.GetServerStatsAsync();
        foreach (var stat in stats)
            Servers.FirstOrDefault(s => s.Id == stat.ServerId)?.UpdateStats(stat);
    }

    private async Task AddNewRowAsync()
    {
        var server = await _ipcClient.AddServerAsync("新服务器", "0.0.0.0");
        if (server != null)
            Servers.Add(ServerViewModel.FromModel(server));
        else
            MessageBox.Show("添加服务器失败，请检查服务是否运行。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private async Task RemoveServerAsync(object? parameter)
    {
        if (parameter is not ServerViewModel server)
            return;

        if (await _ipcClient.RemoveServerAsync(server.Id))
        {
            Servers.Remove(server);
            _lastDeletedName = server.Name;
            _lastDeletedHost = server.Host;
            CanUndo = true;
            _undoTimer.Stop();
            _undoTimer.Start();
        }
    }

    private async Task UndoDeleteAsync()
    {
        if (_lastDeletedName == null || _lastDeletedHost == null)
            return;

        var server = await _ipcClient.AddServerAsync(_lastDeletedName, _lastDeletedHost);
        if (server != null)
            Servers.Add(ServerViewModel.FromModel(server));

        ClearUndo();
    }

    private void ClearUndo()
    {
        _undoTimer.Stop();
        CanUndo = false;
        _lastDeletedName = null;
        _lastDeletedHost = null;
    }

    private async Task ToggleServerAsync(object? parameter)
    {
        if (parameter is not ServerViewModel server)
            return;

        server.IsEnabled = !server.IsEnabled;
        var allServers = Servers.Select(s => s.ToModel()).ToList();
        await _ipcClient.UpdateServersAsync(allServers);
    }

    public async Task<bool> SaveServerAsync(ServerViewModel server)
    {
        server.Name = server.Name.Trim();
        server.Host = server.Host.Trim();

        if (string.IsNullOrWhiteSpace(server.Name) || string.IsNullOrWhiteSpace(server.Host))
        {
            MessageBox.Show("名称和地址不能为空。", "输入无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            await RefreshServersAsync();
            return false;
        }

        var allServers = Servers.Select(s => s.ToModel()).ToList();
        if (!await _ipcClient.UpdateServersAsync(allServers))
        {
            MessageBox.Show("保存服务器失败，请检查服务是否运行。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        StatusMessage = $"已保存 {server.Name}";
        return true;
    }

    public async Task<MonitoringSettings> LoadSettingsAsync() => await _ipcClient.GetSettingsAsync();

    public async Task<bool> SaveSettingsAsync(MonitoringSettings settings)
    {
        var saved = await _ipcClient.UpdateSettingsAsync(settings);
        return saved != null;
    }

    public async Task<bool> TestNotificationAsync() => await _ipcClient.TestNotificationAsync();

    private async void ImportFromTerminal()
    {
        try
        {
            var profiles = WindowsTerminalParser.Parse();

            if (profiles.Count == 0)
            {
                MessageBox.Show("未找到 SSH 相关的 Profile。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var existingHosts = Servers.Select(s => s.Host).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newProfiles = profiles
                .Where(p => !existingHosts.Contains(p.Host))
                .GroupBy(p => p.Host, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (newProfiles.Count == 0)
            {
                MessageBox.Show("所有 SSH Profile 的主机地址都已存在，无需导入。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new ImportDialog(newProfiles)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true || dialog.SelectedProfiles.Count == 0)
                return;

            foreach (var profile in dialog.SelectedProfiles)
                await _ipcClient.AddServerAsync(profile.Name, profile.Host);

            await RefreshServersAsync();
        }
        catch (FileNotFoundException ex)
        {
            MessageBox.Show(ex.Message, "未找到配置", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
