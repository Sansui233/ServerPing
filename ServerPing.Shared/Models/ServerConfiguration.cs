namespace ServerPing.Shared.Models;

public class ServerConfiguration
{
    public List<Server> Servers { get; set; } = new();
    public MonitoringSettings Settings { get; set; } = new();
}
