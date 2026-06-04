using System.Windows.Media;
using ServerPing.Shared.Models;

namespace ServerPing.GUI.ViewModels;

public class MinuteBarViewModel
{
    public required Brush Color { get; init; }
    public required string Tooltip { get; init; }
}

public class ServerViewModel : ViewModelBase
{
    private static readonly Brush GreenBrush  = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
    private static readonly Brush YellowBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly Brush RedBrush    = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
    private static readonly Brush GrayBrush   = new SolidColorBrush(Color.FromRgb(0x98, 0xA2, 0xB3));

    static ServerViewModel()
    {
        GreenBrush.Freeze(); YellowBrush.Freeze(); RedBrush.Freeze(); GrayBrush.Freeze();
    }

    private string _id = "";
    private string _name = "";
    private string _host = "";
    private bool _isEnabled = true;
    private ServerStatus _status = ServerStatus.Unknown;
    private DateTime? _lastPingTime;
    private int _consecutiveFailures;
    private PingStatsWindow _lastHourStats = new();
    private MinuteBarViewModel[] _minuteBars = [];
    private bool _isEditingIdentity;

    public string Id { get => _id; set => SetProperty(ref _id, value); }
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    public string Host
    {
        get => _host;
        set
        {
            if (!SetProperty(ref _host, value)) return;
            OnPropertyChanged(nameof(IsPlaceholderHost));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(LastPingTimeText));
            NotifyStatsChanged();
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!SetProperty(ref _isEnabled, value)) return;
            OnPropertyChanged(nameof(EnableIconColor));
            OnPropertyChanged(nameof(StatusDotColor));
        }
    }

    public ServerStatus Status
    {
        get => _status;
        set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(StatusText)); }
    }

    public DateTime? LastPingTime
    {
        get => _lastPingTime;
        set { if (SetProperty(ref _lastPingTime, value)) OnPropertyChanged(nameof(LastPingTimeText)); }
    }

    public int ConsecutiveFailures { get => _consecutiveFailures; set => SetProperty(ref _consecutiveFailures, value); }

    public PingStatsWindow LastHourStats
    {
        get => _lastHourStats;
        set { if (SetProperty(ref _lastHourStats, value)) NotifyStatsChanged(); }
    }

    public MinuteBarViewModel[] MinuteBars
    {
        get => _minuteBars;
        set => SetProperty(ref _minuteBars, value);
    }

    public bool IsEditingIdentity
    {
        get => _isEditingIdentity;
        set => SetProperty(ref _isEditingIdentity, value);
    }

    public bool IsPlaceholderHost => Host is "0.0.0.0" or "127.0.0.1" or "localhost";

    public string StatusText => IsPlaceholderHost ? "—" : Status switch
    {
        ServerStatus.Online => "在线",
        ServerStatus.Offline => "离线",
        _ => "未知"
    };

    public string LastPingTimeText => IsPlaceholderHost ? "—" : LastPingTime?.ToString("HH:mm:ss") ?? "-";

    public string LastHourAvailabilityText => IsPlaceholderHost ? "—" : FormatAvailability(LastHourStats.AvailabilityPercent);

    public Brush StatusDotColor => ComputeAvailabilityBrush(LastHourStats);

    public Brush AvailabilityColor => ComputeAvailabilityBrush(LastHourStats);

    public Brush EnableIconColor => IsEnabled ? GreenBrush : GrayBrush;

    private Brush ComputeAvailabilityBrush(PingStatsWindow stats)
    {
        if (!IsEnabled || IsPlaceholderHost) return GrayBrush;
        var pct = stats.AvailabilityPercent;
        if (!pct.HasValue) return GrayBrush;
        return pct.Value switch
        {
            >= 90 => GreenBrush,
            >= 80 => YellowBrush,
            _ => RedBrush
        };
    }

    private static Brush ComputeMinuteBrush(MinuteStats m)
    {
        var total = m.SuccessCount + m.FailureCount;
        if (total == 0) return GrayBrush;
        var pct = m.SuccessCount * 100.0 / total;
        return pct switch
        {
            >= 90 => GreenBrush,
            >= 80 => YellowBrush,
            _ => RedBrush
        };
    }

    private static string FormatAvailability(double? value) =>
        value.HasValue ? $"{value.Value:0.#}%" : "—";

    private void NotifyStatsChanged()
    {
        OnPropertyChanged(nameof(LastHourAvailabilityText));
        OnPropertyChanged(nameof(AvailabilityColor));
        OnPropertyChanged(nameof(StatusDotColor));
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
        if (!IsEditingIdentity)
        {
            Name = server.Name;
            Host = server.Host;
        }

        Status = server.Status;
        LastPingTime = server.LastPingTime;
        ConsecutiveFailures = server.ConsecutiveFailures;
        IsEnabled = server.IsEnabled;
    }

    public void UpdateStats(ServerStats stats)
    {
        LastHourStats = stats.LastHour;
        MinuteBars = stats.RecentMinutes
            .Select(m => new MinuteBarViewModel
            {
                Color = ComputeMinuteBrush(m),
                Tooltip = m.SuccessCount + m.FailureCount == 0
                    ? ""
                    : $"成功：{m.SuccessCount} 失败：{m.FailureCount}"
            })
            .ToArray();
    }
}
