namespace ServerPing.Shared.Models;

public class Server
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Host { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastPingTime { get; set; }
    public long? LastLatencyMilliseconds { get; set; }
    public ServerStatus Status { get; set; } = ServerStatus.Unknown;
    public int ConsecutiveFailures { get; set; }
}
