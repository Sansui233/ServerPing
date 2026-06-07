using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Threading;
using ServerPing.GUI.Services;
using ServerPing.GUI.ViewModels;
using ServerPing.Shared;

namespace ServerPing.GUI.Views.ServerStatsOverlay;

public class ServerStatsOverlayViewModel : ViewModelBase, IDisposable
{
    private static readonly Brush GreenBrush = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
    private static readonly Brush YellowBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly Brush RedBrush = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
    private static readonly Brush EmptyBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
    private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128));

    private readonly DispatcherTimer _timer;
    private readonly string _statsFilePath;
    private DateTime _lastWriteTimeUtc = DateTime.MinValue;
    private DateTime _anchorDate = DateTime.Today;
    private long _lastFileLength = -1;
    private bool _disposed;
    private string _serverName;
    private string _host;
    private string _currentAvailabilityText;
    private string _currentLatencyText;
    private Brush _currentAvailabilityColor = EmptyBrush;
    private Brush _currentLatencyColor = MutedBrush;
    private string _totalSuccessText = "0";
    private string _totalFailureText = "0";
    private string _totalAvailabilityText = "—";
    private Brush _totalAvailabilityColor = EmptyBrush;

    public ServerStatsOverlayViewModel(ServerViewModel server, Action close)
    {
        ServerId = server.Id;
        _serverName = server.Name;
        _host = server.Host;
        _currentAvailabilityText = server.LastHourAvailabilityText;
        _currentLatencyText = server.LatencyText;
        _currentAvailabilityColor = server.AvailabilityColor;
        _currentLatencyColor = server.LatencyColor;
        CloseCommand = new RelayCommand(close);
        Cells = new ObservableCollection<HeatmapCellViewModel>(CreateEmptyCells());
        DateLabels = new ObservableCollection<string>(BuildDateLabels());
        _statsFilePath = Path.Combine(ConfigurationManager.ConfigDirectory, "stats", $"{ServerId}.json");
        LoadFromDisk(force: true);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => LoadFromDisk(force: false);
        _timer.Start();
    }

    public string ServerId { get; }
    public RelayCommand CloseCommand { get; }
    public ObservableCollection<HeatmapCellViewModel> Cells { get; }
    public ObservableCollection<string> DateLabels { get; }

    public string ServerName
    {
        get => _serverName;
        private set => SetProperty(ref _serverName, value);
    }

    public string Host
    {
        get => _host;
        private set => SetProperty(ref _host, value);
    }

    public string CurrentAvailabilityText
    {
        get => _currentAvailabilityText;
        private set => SetProperty(ref _currentAvailabilityText, value);
    }

    public string CurrentLatencyText
    {
        get => _currentLatencyText;
        private set => SetProperty(ref _currentLatencyText, value);
    }

    public Brush CurrentAvailabilityColor
    {
        get => _currentAvailabilityColor;
        private set => SetProperty(ref _currentAvailabilityColor, value);
    }

    public Brush CurrentLatencyColor
    {
        get => _currentLatencyColor;
        private set => SetProperty(ref _currentLatencyColor, value);
    }

    public string TotalSuccessText
    {
        get => _totalSuccessText;
        private set => SetProperty(ref _totalSuccessText, value);
    }

    public string TotalFailureText
    {
        get => _totalFailureText;
        private set => SetProperty(ref _totalFailureText, value);
    }

    public string TotalAvailabilityText
    {
        get => _totalAvailabilityText;
        private set => SetProperty(ref _totalAvailabilityText, value);
    }

    public Brush TotalAvailabilityColor
    {
        get => _totalAvailabilityColor;
        private set => SetProperty(ref _totalAvailabilityColor, value);
    }

    public void UpdateLiveServer(ServerViewModel server)
    {
        ServerName = server.Name;
        Host = server.Host;
        CurrentAvailabilityText = server.LastHourAvailabilityText;
        CurrentLatencyText = server.LatencyText;
        CurrentAvailabilityColor = server.AvailabilityColor;
        CurrentLatencyColor = server.LatencyColor;
    }

    public void RefreshLocalizedText()
    {
        foreach (var cell in Cells)
            cell.RefreshTooltip();
    }

    private void LoadFromDisk(bool force)
    {
        if (_disposed)
            return;

        if (_anchorDate != DateTime.Today)
        {
            _anchorDate = DateTime.Today;
            RebuildCalendar();
            force = true;
        }

        var fileInfo = new FileInfo(_statsFilePath);
        if (!fileInfo.Exists)
        {
            if (force)
                ApplyStats(new Dictionary<string, HourAggregateSnapshot>());
            return;
        }

        if (!force && fileInfo.LastWriteTimeUtc == _lastWriteTimeUtc && fileInfo.Length == _lastFileLength)
            return;

        _lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        _lastFileLength = fileInfo.Length;

        try
        {
            var json = File.ReadAllText(_statsFilePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, HourAggregateSnapshot>>(json) ?? [];
            ApplyStats(data);
        }
        catch
        {
            ApplyStats(new Dictionary<string, HourAggregateSnapshot>());
        }
    }

    private void ApplyStats(IReadOnlyDictionary<string, HourAggregateSnapshot> data)
    {
        var today = DateTime.Today;
        var totalSuccess = 0;
        var totalFailure = 0;

        for (var dayOffset = 6; dayOffset >= 0; dayOffset--)
        {
            var date = today.AddDays(-dayOffset);
            for (var hour = 0; hour < 24; hour++)
            {
                var index = (6 - dayOffset) * 24 + hour;
                var key = $"{date:yyyy-MM-dd}-{hour:00}-{(hour + 1) % 24:00}";
                data.TryGetValue(key, out var aggregate);
                var success = aggregate?.Success ?? 0;
                var failure = aggregate?.Failure ?? 0;
                Cells[index].Update(date, hour, success, failure);
                totalSuccess += success;
                totalFailure += failure;
            }
        }

        var total = totalSuccess + totalFailure;
        var availability = total == 0 ? (double?)null : totalSuccess * 100.0 / total;
        TotalSuccessText = totalSuccess.ToString();
        TotalFailureText = totalFailure.ToString();
        TotalAvailabilityText = FormatAvailability(availability);
        TotalAvailabilityColor = ComputeAvailabilityBrush(availability);
    }

    private static IEnumerable<HeatmapCellViewModel> CreateEmptyCells()
    {
        var today = DateTime.Today;
        for (var dayOffset = 6; dayOffset >= 0; dayOffset--)
        {
            var date = today.AddDays(-dayOffset);
            for (var hour = 0; hour < 24; hour++)
                yield return new HeatmapCellViewModel(date, hour);
        }
    }

    private static IEnumerable<string> BuildDateLabels()
    {
        var today = DateTime.Today;
        for (var dayOffset = 6; dayOffset >= 0; dayOffset--)
            yield return today.AddDays(-dayOffset).ToString("MM-dd");
    }

    private void RebuildCalendar()
    {
        Cells.Clear();
        foreach (var cell in CreateEmptyCells())
            Cells.Add(cell);

        DateLabels.Clear();
        foreach (var label in BuildDateLabels())
            DateLabels.Add(label);
    }

    private static Brush ComputeAvailabilityBrush(double? pct)
    {
        if (!pct.HasValue)
            return EmptyBrush;
        if (pct.Value > 90)
            return GreenBrush;
        return pct.Value >= 80 ? YellowBrush : RedBrush;
    }

    private static string FormatAvailability(double? value) =>
        value.HasValue ? $"{value.Value:0.#}%" : "—";

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer.Stop();
    }

    private class HourAggregateSnapshot
    {
        public int Success { get; set; }
        public int Failure { get; set; }
    }

    public class HeatmapCellViewModel : ViewModelBase
    {
        private DateTime _date;
        private int _hour;
        private int _successCount;
        private int _failureCount;
        private Brush _color = EmptyBrush;
        private string _tooltip = string.Empty;
        private bool _hasData;

        public HeatmapCellViewModel(DateTime date, int hour)
        {
            _date = date;
            _hour = hour;
            Refresh();
        }

        public Brush Color
        {
            get => _color;
            private set => SetProperty(ref _color, value);
        }

        public string Tooltip
        {
            get => _tooltip;
            private set => SetProperty(ref _tooltip, value);
        }

        public bool HasData
        {
            get => _hasData;
            private set => SetProperty(ref _hasData, value);
        }

        public void Update(DateTime date, int hour, int success, int failure)
        {
            if (_date == date && _hour == hour && _successCount == success && _failureCount == failure)
                return;

            _date = date;
            _hour = hour;
            _successCount = success;
            _failureCount = failure;
            Refresh();
        }

        public void RefreshTooltip() => Refresh();

        private void Refresh()
        {
            var total = _successCount + _failureCount;
            var availability = total == 0 ? (double?)null : _successCount * 100.0 / total;
            HasData = total > 0;
            Color = total == 0 ? EmptyBrush : ComputeAvailabilityBrush(availability);
            Tooltip = LocalizationService.Format(
                "StatsOverlay.CellTooltip",
                _date.ToString("MM-dd"),
                $"{_hour:00}:00",
                _successCount,
                _failureCount);
        }
    }
}
