using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ServerPing.GUI.Services;

namespace ServerPing.GUI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IpcClient _ipcClient = new();
    private readonly DispatcherTimer _refreshTimer;
    private ServerViewModel? _selectedServer;
    private string _statusMessage = "正在连接服务...";
    private bool _isConnected;
    private string _newServerName = "";
    private string _newServerHost = "";

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

    public string NewServerName
    {
        get => _newServerName;
        set => SetProperty(ref _newServerName, value);
    }

    public string NewServerHost
    {
        get => _newServerHost;
        set => SetProperty(ref _newServerHost, value);
    }

    public RelayCommand AddServerCommand { get; }
    public RelayCommand RemoveServerCommand { get; }
    public RelayCommand ToggleServerCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand ImportFromTerminalCommand { get; }

    public MainViewModel()
    {
        AddServerCommand = new RelayCommand(async () => await AddServerAsync(),
            () => !string.IsNullOrWhiteSpace(NewServerName) && !string.IsNullOrWhiteSpace(NewServerHost));
        RemoveServerCommand = new RelayCommand(async (p) => await RemoveServerAsync(p),
            (p) => p is ServerViewModel);
        ToggleServerCommand = new RelayCommand(async (p) => await ToggleServerAsync(p),
            (p) => p is ServerViewModel);
        RefreshCommand = new RelayCommand(async () => await RefreshServersAsync());
        ImportFromTerminalCommand = new RelayCommand(ImportFromTerminal);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _refreshTimer.Tick += async (s, e) => await RefreshServersAsync();
    }

    public async Task InitializeAsync()
    {
        await RefreshServersAsync();
        _refreshTimer.Start();
    }

    public void Shutdown()
    {
        _refreshTimer.Stop();
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
                {
                    existing.UpdateFrom(serverModel);
                }
                else
                {
                    Servers.Add(ServerViewModel.FromModel(serverModel));
                }
            }

            var serverIds = servers.Select(s => s.Id).ToHashSet();
            var toRemove = Servers.Where(s => !serverIds.Contains(s.Id)).ToList();
            foreach (var s in toRemove)
            {
                Servers.Remove(s);
            }

            var onlineCount = servers.Count(s => s.Status == Shared.Models.ServerStatus.Online);
            StatusMessage = $"已连接 - {onlineCount}/{servers.Count} 在线";
            IsConnected = true;
        }
        catch
        {
            StatusMessage = "无法连接到服务，请确认 ServerPing Service 正在运行";
            IsConnected = false;
        }
    }

    private async Task AddServerAsync()
    {
        var name = NewServerName.Trim();
        var host = NewServerHost.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(host))
            return;

        var server = await _ipcClient.AddServerAsync(name, host);
        if (server != null)
        {
            Servers.Add(ServerViewModel.FromModel(server));
            NewServerName = "";
            NewServerHost = "";
        }
        else
        {
            MessageBox.Show("添加服务器失败，请检查服务是否运行。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RemoveServerAsync(object? parameter)
    {
        if (parameter is not ServerViewModel server)
            return;

        var result = MessageBox.Show($"确认删除服务器 \"{server.Name}\" ({server.Host})?",
            "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        if (await _ipcClient.RemoveServerAsync(server.Id))
        {
            Servers.Remove(server);
        }
    }

    private async Task ToggleServerAsync(object? parameter)
    {
        if (parameter is not ServerViewModel server)
            return;

        server.IsEnabled = !server.IsEnabled;

        var allServers = Servers.Select(s => s.ToModel()).ToList();
        await _ipcClient.UpdateServersAsync(allServers);
    }

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
            var dialog = new ImportDialog(profiles, existingHosts)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true || dialog.SelectedProfiles.Count == 0)
                return;

            foreach (var profile in dialog.SelectedProfiles)
            {
                await _ipcClient.AddServerAsync(profile.Name, profile.Host);
            }

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
