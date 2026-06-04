namespace ServerPing.Shared.Models;

public class MonitoringSettings
{
    public const int MinPingIntervalSeconds = 1;
    public const int MaxPingIntervalSeconds = 300;
    public const int MinFailureThreshold = 1;
    public const int MaxFailureThreshold = 20;
    public const int MinGuiHibernateDuration = 5;
    public const int MaxGuiHibernateDuration = 120;

    private int _pingIntervalSeconds = 5;
    private int _failureThreshold = 3;
    private int _guiHibernateDurationSeconds = 10;
    private string _language = "system";

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

    public bool LaunchAtStartup { get; set; }

    public string Language
    {
        get => _language;
        set => _language = string.IsNullOrWhiteSpace(value) ? "system" : value;
    }

    public int GuiHibernateDurationSeconds
    {
        get => _guiHibernateDurationSeconds;
        set => _guiHibernateDurationSeconds = Math.Clamp(value, MinGuiHibernateDuration, MaxGuiHibernateDuration);
    }

    public MonitoringSettings Clone() => new()
    {
        PingIntervalSeconds = PingIntervalSeconds,
        FailureThreshold = FailureThreshold,
        SilentStartup = SilentStartup,
        LaunchAtStartup = LaunchAtStartup,
        Language = Language,
        GuiHibernateDurationSeconds = GuiHibernateDurationSeconds
    };
}
