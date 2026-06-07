using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ServerPing.GUI.Models;
using ServerPing.GUI.Services;
using ServerPing.GUI.Views.ServerStatsOverlay;
using ServerPing.Shared.Models;

namespace ServerPing.GUI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IpcClient _ipcClient = new();
    private readonly GuiStateStore _guiStateStore = new();
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _undoTimer;
    private readonly List<string> _customOrderIds = [];
    private readonly List<string> _serviceOrderIds = [];
    private ServerViewModel? _selectedServer;
    private ServerViewModel? _selectedStatsServer;
    private ServerStatsOverlayViewModel? _selectedStatsOverlay;
    private string _statusMessage = LocalizationService.Get("Status.Connecting");
    private bool _isConnected;
    private int _lastOnlineCount;
    private int _lastTotalCount;
    private LocalNetworkStatus _lastLocalNetworkStatus = LocalNetworkStatus.Unknown;
    private bool _canUndo;
    private bool _isHostVisible = true;
    private ServerSortMode _nameSortMode = ServerSortMode.Auto;
    private string? _lastDeletedName;
    private string? _lastDeletedHost;

    public ObservableCollection<ServerViewModel> Servers { get; } = [];

    public ServerViewModel? SelectedServer
    {
        get => _selectedServer;
        set => SetProperty(ref _selectedServer, value);
    }

    public ServerViewModel? SelectedStatsServer
    {
        get => _selectedStatsServer;
        private set => SetProperty(ref _selectedStatsServer, value);
    }

    public ServerStatsOverlayViewModel? SelectedStatsOverlay
    {
        get => _selectedStatsOverlay;
        private set
        {
            if (_selectedStatsOverlay == value)
                return;

            _selectedStatsOverlay?.Dispose();
            SetProperty(ref _selectedStatsOverlay, value);
            OnPropertyChanged(nameof(IsStatsOverlayVisible));
        }
    }

    public bool IsStatsOverlayVisible => SelectedStatsOverlay != null;

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

    public bool IsHostVisible
    {
        get => _isHostVisible;
        set
        {
            if (!SetProperty(ref _isHostVisible, value))
                return;

            OnPropertyChanged(nameof(HostVisibilityToolTip));
        }
    }

    public string HostVisibilityToolTip => IsHostVisible
        ? LocalizationService.Get("Main.HideHost")
        : LocalizationService.Get("Main.ShowHost");

    public string NameSortModeLabel => _nameSortMode switch
    {
        ServerSortMode.AToZ => LocalizationService.Get("Main.SortAtoZ"),
        ServerSortMode.ZToA => LocalizationService.Get("Main.SortZtoA"),
        _ => ""
    };

    public bool IsNameSortModeLabelVisible => _nameSortMode != ServerSortMode.Auto;

    public ServerSortMode NameSortMode => _nameSortMode;

    public RelayCommand AddServerCommand { get; }
    public RelayCommand RemoveServerCommand { get; }
    public RelayCommand ToggleServerCommand { get; }
    public RelayCommand ImportFromTerminalCommand { get; }
    public RelayCommand UndoDeleteCommand { get; }
    public RelayCommand ToggleHostVisibilityCommand { get; }
    public RelayCommand OpenStatsOverlayCommand { get; }
    public RelayCommand CloseStatsOverlayCommand { get; }

    public MainViewModel()
    {
        AddServerCommand = new RelayCommand(async () => await AddNewRowAsync());
        RemoveServerCommand = new RelayCommand(async (p) => await RemoveServerAsync(p),
            (p) => p is ServerViewModel);
        ToggleServerCommand = new RelayCommand(async (p) => await ToggleServerAsync(p),
            (p) => p is ServerViewModel);
        ImportFromTerminalCommand = new RelayCommand(ImportFromTerminal);
        UndoDeleteCommand = new RelayCommand(async () => await UndoDeleteAsync(), () => CanUndo);
        ToggleHostVisibilityCommand = new RelayCommand(() => IsHostVisible = !IsHostVisible);
        OpenStatsOverlayCommand = new RelayCommand(OpenStatsOverlay, p => p is ServerViewModel);
        CloseStatsOverlayCommand = new RelayCommand(CloseStatsOverlay);

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += async (s, e) => await RefreshServersAsync();

        _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _undoTimer.Tick += (s, e) => ClearUndo();
    }

    public async Task InitializeAsync()
    {
        LoadGuiState();
        await RefreshServersAsync();
        _refreshTimer.Start();
    }

    public void Shutdown()
    {
        _refreshTimer.Stop();
        _undoTimer.Stop();
        SelectedStatsOverlay = null;
    }

    private async Task RefreshServersAsync()
    {
        try
        {
            var servers = await _ipcClient.GetServersAsync();
            SyncServerOrders(servers);

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
            {
                if (SelectedStatsServer?.Id == s.Id)
                    CloseStatsOverlay();
                Servers.Remove(s);
            }

            if (!Servers.Any(s => s.IsEditingIdentity))
                ApplyDisplayOrder();

            var status = await _ipcClient.GetStatusAsync()
                ?? new ServiceStatus
                {
                    OnlineCount = servers.Count(s => s.IsEnabled && s.Status == ServerStatus.Online),
                    TotalCount = servers.Count(s => s.IsEnabled),
                    LocalNetworkStatus = LocalNetworkStatus.Unknown
                };

            IsConnected = true;
            UpdateStatusMessage(status);

            await RefreshStatsAsync();
            UpdateSelectedStatsOverlay();
        }
        catch
        {
            StatusMessage = LocalizationService.Get("Status.ServiceUnavailable");
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
        var server = await _ipcClient.AddServerAsync(LocalizationService.Get("Server.NewServer"), "0.0.0.0");
        if (server != null)
        {
            _customOrderIds.Add(server.Id);
            _serviceOrderIds.Add(server.Id);
            SaveGuiState();
            Servers.Add(ServerViewModel.FromModel(server));
            ApplyDisplayOrder();
        }
        else
            ShowMessage("Message.AddServerFailed", "Dialog.Error", MessageBoxImage.Error);
    }

    private async Task RemoveServerAsync(object? parameter)
    {
        if (parameter is not ServerViewModel server)
            return;

        if (await _ipcClient.RemoveServerAsync(server.Id))
        {
            if (SelectedStatsServer?.Id == server.Id)
                CloseStatsOverlay();
            Servers.Remove(server);
            _customOrderIds.Remove(server.Id);
            _serviceOrderIds.Remove(server.Id);
            SaveGuiState();
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
        {
            _customOrderIds.Add(server.Id);
            _serviceOrderIds.Add(server.Id);
            SaveGuiState();
            Servers.Add(ServerViewModel.FromModel(server));
            ApplyDisplayOrder();
        }

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
        var allServers = GetServersInServiceOrder();
        await _ipcClient.UpdateServersAsync(allServers);
    }

    public void CycleNameSortMode()
    {
        _nameSortMode = _nameSortMode switch
        {
            ServerSortMode.Auto => ServerSortMode.AToZ,
            ServerSortMode.AToZ => ServerSortMode.ZToA,
            _ => ServerSortMode.Auto
        };

        NotifySortModeChanged();
        SaveGuiState();
        ApplyDisplayOrder();
    }

    public void ResetNameSortMode()
    {
        if (_nameSortMode == ServerSortMode.Auto)
            return;

        _nameSortMode = ServerSortMode.Auto;
        NotifySortModeChanged();
        SaveGuiState();
    }

    public void UseCustomOrder(IReadOnlyList<ServerViewModel> orderedServers)
    {
        if (orderedServers.Count != Servers.Count || orderedServers.Distinct().Count() != Servers.Count)
            return;

        if (orderedServers.Any(server => !Servers.Contains(server)))
            return;

        for (var targetIndex = 0; targetIndex < orderedServers.Count; targetIndex++)
        {
            var currentIndex = Servers.IndexOf(orderedServers[targetIndex]);
            if (currentIndex >= 0 && currentIndex != targetIndex)
                Servers.Move(currentIndex, targetIndex);
        }

        _nameSortMode = ServerSortMode.Auto;
        NotifySortModeChanged();
        RebuildCustomOrderFromCurrentView();
        SaveGuiState();
    }

    public Task<bool> MoveServerAsync(ServerViewModel server, int insertIndex)
    {
        if (!Servers.Contains(server))
            return Task.FromResult(false);

        insertIndex = Math.Clamp(insertIndex, 0, Servers.Count);
        var oldIndex = Servers.IndexOf(server);
        if (oldIndex < insertIndex)
            insertIndex--;

        if (oldIndex == insertIndex)
            return Task.FromResult(true);

        Servers.Move(oldIndex, insertIndex);
        _nameSortMode = ServerSortMode.Auto;
        NotifySortModeChanged();
        RebuildCustomOrderFromCurrentView();
        SaveGuiState();
        return Task.FromResult(true);
    }

    public async Task<bool> SaveServerAsync(ServerViewModel server)
    {
        server.Name = server.Name.Trim();
        server.Host = server.Host.Trim();

        if (string.IsNullOrWhiteSpace(server.Name) || string.IsNullOrWhiteSpace(server.Host))
        {
            ShowMessage("Message.NameHostRequired", "Dialog.InvalidInput", MessageBoxImage.Warning);
            await RefreshServersAsync();
            return false;
        }

        var allServers = GetServersInServiceOrder();
        if (!await _ipcClient.UpdateServersAsync(allServers))
        {
            ShowMessage("Message.SaveServerFailed", "Dialog.Error", MessageBoxImage.Error);
            return false;
        }

        StatusMessage = LocalizationService.Format("Status.Saved", server.Name);
        ApplyDisplayOrder();
        return true;
    }

    public async Task<MonitoringSettings> LoadSettingsAsync() => await _ipcClient.GetSettingsAsync();

    public async Task<bool> SaveSettingsAsync(MonitoringSettings settings)
    {
        var saved = await _ipcClient.UpdateSettingsAsync(settings);
        return saved != null;
    }

    public async Task<bool> TestNotificationAsync() => await _ipcClient.TestNotificationAsync();

    public async Task<bool> TestNotificationSoundAsync() => await _ipcClient.TestNotificationSoundAsync();

    private async void ImportFromTerminal()
    {
        try
        {
            var profiles = WindowsTerminalParser.Parse();

            if (profiles.Count == 0)
            {
                ShowMessage("Message.NoSshProfiles", "Dialog.Info", MessageBoxImage.Information);
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
                ShowMessage("Message.AllProfilesExist", "Dialog.Info", MessageBoxImage.Information);
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
            ThemeMessageBox.Show(ex.Message, LocalizationService.Get("Dialog.ConfigNotFound"), MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            ThemeMessageBox.Show(LocalizationService.Format("Message.ImportFailed", ex.Message), LocalizationService.Get("Dialog.Error"), MessageBoxImage.Error);
        }
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(HostVisibilityToolTip));
        RefreshStatusMessageText();
        NotifySortModeChanged();
        foreach (var server in Servers)
            server.RefreshLocalizedText();
        SelectedStatsOverlay?.RefreshLocalizedText();
    }

    private void UpdateStatusMessage(ServiceStatus status)
    {
        _lastOnlineCount = status.OnlineCount;
        _lastTotalCount = status.TotalCount;
        _lastLocalNetworkStatus = status.LocalNetworkStatus;
        RefreshStatusMessageText();
    }

    private void RefreshStatusMessageText()
    {
        if (!IsConnected)
            return;

        StatusMessage = _lastLocalNetworkStatus == LocalNetworkStatus.NoNetwork
            ? LocalizationService.Get("Status.LocalNetworkUnavailable")
            : LocalizationService.Format("Status.Connected", _lastOnlineCount, _lastTotalCount);
    }

    private void OpenStatsOverlay(object? parameter)
    {
        if (parameter is not ServerViewModel server || server.IsPlaceholderHost)
            return;

        SelectedStatsServer = server;
        SelectedStatsOverlay = new ServerStatsOverlayViewModel(server, CloseStatsOverlay);
    }

    private void CloseStatsOverlay()
    {
        SelectedStatsServer = null;
        SelectedStatsOverlay = null;
    }

    private void UpdateSelectedStatsOverlay()
    {
        if (SelectedStatsServer == null || SelectedStatsOverlay == null)
            return;

        var current = Servers.FirstOrDefault(s => s.Id == SelectedStatsServer.Id);
        if (current == null)
        {
            CloseStatsOverlay();
            return;
        }

        SelectedStatsServer = current;
        SelectedStatsOverlay.UpdateLiveServer(current);
    }

    private void LoadGuiState()
    {
        var state = _guiStateStore.Load();
        _nameSortMode = state.NameSortMode;
        _customOrderIds.Clear();
        _customOrderIds.AddRange(state.ServerOrderIds.Distinct());
        NotifySortModeChanged();
    }

    private void SaveGuiState()
    {
        _guiStateStore.Save(new GuiState
        {
            NameSortMode = _nameSortMode,
            ServerOrderIds = _customOrderIds.ToList()
        });
    }

    private void SyncServerOrders(List<Server> servers)
    {
        _serviceOrderIds.Clear();
        _serviceOrderIds.AddRange(servers.Select(s => s.Id).Distinct());

        var changed = false;
        var serverIds = _serviceOrderIds.ToHashSet();
        changed |= _customOrderIds.RemoveAll(id => !serverIds.Contains(id)) > 0;

        foreach (var id in _serviceOrderIds)
        {
            if (_customOrderIds.Contains(id))
                continue;

            _customOrderIds.Add(id);
            changed = true;
        }

        if (changed)
            SaveGuiState();
    }

    private void ApplyDisplayOrder()
    {
        var ordered = _nameSortMode switch
        {
            ServerSortMode.AToZ => Servers
                .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(s => s.Host, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            ServerSortMode.ZToA => Servers
                .OrderByDescending(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(s => s.Host, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            _ => GetServersInCustomOrderView()
        };

        for (var targetIndex = 0; targetIndex < ordered.Count; targetIndex++)
        {
            var currentIndex = Servers.IndexOf(ordered[targetIndex]);
            if (currentIndex >= 0 && currentIndex != targetIndex)
                Servers.Move(currentIndex, targetIndex);
        }
    }

    private List<ServerViewModel> GetServersInCustomOrderView()
    {
        var byId = Servers.ToDictionary(s => s.Id);
        var usedIds = new HashSet<string>();
        var ordered = new List<ServerViewModel>();

        foreach (var id in _customOrderIds)
        {
            if (byId.TryGetValue(id, out var server))
            {
                ordered.Add(server);
                usedIds.Add(id);
            }
        }

        ordered.AddRange(Servers.Where(s => !usedIds.Contains(s.Id)));
        return ordered;
    }

    private List<Server> GetServersInServiceOrder()
    {
        var byId = Servers.ToDictionary(s => s.Id);
        var usedIds = new HashSet<string>();
        var ordered = new List<Server>();

        foreach (var id in _serviceOrderIds)
        {
            if (byId.TryGetValue(id, out var server))
            {
                ordered.Add(server.ToModel());
                usedIds.Add(id);
            }
        }

        ordered.AddRange(Servers.Where(s => !usedIds.Contains(s.Id)).Select(s => s.ToModel()));
        return ordered;
    }

    private void RebuildCustomOrderFromCurrentView()
    {
        _customOrderIds.Clear();
        _customOrderIds.AddRange(Servers.Select(s => s.Id));
    }

    private void NotifySortModeChanged()
    {
        OnPropertyChanged(nameof(NameSortMode));
        OnPropertyChanged(nameof(NameSortModeLabel));
        OnPropertyChanged(nameof(IsNameSortModeLabelVisible));
    }

    private static void ShowMessage(string messageKey, string captionKey, MessageBoxImage image) =>
        ThemeMessageBox.ShowLocalized(messageKey, captionKey, image);
}
