namespace ServerPing.Shared.Models;

public class ServiceStatus
{
    public int OnlineCount { get; set; }
    public int TotalCount { get; set; }
    public LocalNetworkStatus LocalNetworkStatus { get; set; } = LocalNetworkStatus.Unknown;
}
