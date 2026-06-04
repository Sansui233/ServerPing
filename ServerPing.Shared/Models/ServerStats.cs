namespace ServerPing.Shared.Models;

public class ServerStats
{
    public required string ServerId { get; set; }
    public PingStatsWindow LastHour { get; set; } = new();
    public PingStatsWindow LastDay { get; set; } = new();
}

public class PingStatsWindow
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int TotalCount => SuccessCount + FailureCount;
    public double? AvailabilityPercent => TotalCount == 0
        ? null
        : SuccessCount * 100.0 / TotalCount;
}
