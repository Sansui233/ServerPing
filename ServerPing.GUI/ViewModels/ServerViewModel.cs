using ServerPing.Shared.Models;

namespace ServerPing.GUI.ViewModels;

public class ServerViewModel : ViewModelBase
{
    private string _id = "";
    private string _name = "";
    private string _host = "";
    private bool _isEnabled = true;
    private ServerStatus _status = ServerStatus.Unknown;
    private DateTime? _lastPingTime;
    private int _consecutiveFailures;
    private PingStatsWindow _lastHourStats = new();
    private PingStatsWindow _lastDayStats = new();

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Host
    {
        get => _host;
        set => SetProperty(ref _host, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public ServerStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
                OnPropertyChanged(nameof(StatusText));
        }
    }

    public DateTime? LastPingTime
    {
        get => _lastPingTime;
        set
        {
            if (SetProperty(ref _lastPingTime, value))
                OnPropertyChanged(nameof(LastPingTimeText));
        }
    }

    public int ConsecutiveFailures
    {
        get => _consecutiveFailures;
        set => SetProperty(ref _consecutiveFailures, value);
    }

    public PingStatsWindow LastHourStats
    {
        get => _lastHourStats;
        set
        {
            if (SetProperty(ref _lastHourStats, value))
                NotifyStatsChanged();
        }
    }

    public PingStatsWindow LastDayStats
    {
        get => _lastDayStats;
        set
        {
            if (SetProperty(ref _lastDayStats, value))
                NotifyStatsChanged();
        }
    }

    public string StatusText => Status switch
    {
        ServerStatus.Online => "在线",
        ServerStatus.Offline => "离线",
        _ => "未知"
    };

    public string LastPingTimeText => LastPingTime?.ToString("HH:mm:ss") ?? "-";
    public string LastHourStatsText => FormatStats(LastHourStats);
    public string LastDayStatsText => FormatStats(LastDayStats);
    public string LastHourAvailabilityText => FormatAvailability(LastHourStats.AvailabilityPercent);

    private static string FormatStats(PingStatsWindow stats) =>
        stats.TotalCount == 0
            ? "0 / 0 / 0"
            : $"{stats.SuccessCount} / {stats.FailureCount} / {stats.TotalCount}";

    private static string FormatAvailability(double? value) =>
        value.HasValue ? $"{value.Value:0.#}%" : "--";

    private void NotifyStatsChanged()
    {
        OnPropertyChanged(nameof(LastHourStatsText));
        OnPropertyChanged(nameof(LastDayStatsText));
        OnPropertyChanged(nameof(LastHourAvailabilityText));
    }

    public static ServerViewModel FromModel(Server server) => new()
    {
        Id = server.Id,
        Name = server.Name,
        Host = server.Host,
        IsEnabled = server.IsEnabled,
        Status = server.Status,
        LastPingTime = server.LastPingTime,
        ConsecutiveFailures = server.ConsecutiveFailures
    };

    public Server ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Host = Host,
        IsEnabled = IsEnabled,
        Status = Status,
        LastPingTime = LastPingTime,
        ConsecutiveFailures = ConsecutiveFailures
    };

    public void UpdateFrom(Server server)
    {
        Name = server.Name;
        Host = server.Host;
        Status = server.Status;
        LastPingTime = server.LastPingTime;
        ConsecutiveFailures = server.ConsecutiveFailures;
        IsEnabled = server.IsEnabled;
    }

    public void UpdateStats(ServerStats stats)
    {
        LastHourStats = stats.LastHour;
        LastDayStats = stats.LastDay;
    }
}
