namespace ServerPing.Shared.Models;

public class MonitoringSettings
{
    public const int MinPingIntervalSeconds = 1;
    public const int MaxPingIntervalSeconds = 300;
    public const int MinFailureThreshold = 1;
    public const int MaxFailureThreshold = 20;

    private int _pingIntervalSeconds = 5;
    private int _failureThreshold = 3;

    public int PingIntervalSeconds
    {
        get => _pingIntervalSeconds;
        set => _pingIntervalSeconds = Math.Clamp(value, MinPingIntervalSeconds, MaxPingIntervalSeconds);
    }

    public int FailureThreshold
    {
        get => _failureThreshold;
        set => _failureThreshold = Math.Clamp(value, MinFailureThreshold, MaxFailureThreshold);
    }

    public bool SilentStartup { get; set; }

    public MonitoringSettings Clone() => new()
    {
        PingIntervalSeconds = PingIntervalSeconds,
        FailureThreshold = FailureThreshold,
        SilentStartup = SilentStartup
    };
}
