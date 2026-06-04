using System.Net.NetworkInformation;
using ServerPing.Shared.Models;

namespace ServerPing.Service;

public class PingService : IDisposable
{
    private readonly Dictionary<string, System.Threading.Timer> _timers = new();
    private readonly Dictionary<string, Server> _servers = new();
    private readonly object _lock = new();

    public event EventHandler<ServerStatusChangedEventArgs>? StatusChanged;

    public void Start(List<Server> servers)
    {
        lock (_lock)
        {
            foreach (var server in servers)
            {
                _servers[server.Id] = server;

                if (server.IsEnabled)
                {
                    StartPinging(server);
                }
            }
        }
    }

    public void UpdateServers(List<Server> servers)
    {
        lock (_lock)
        {
            var existingIds = _servers.Keys.ToHashSet();
            var newIds = servers.Select(s => s.Id).ToHashSet();

            var toRemove = existingIds.Except(newIds).ToList();
            foreach (var id in toRemove)
            {
                StopPinging(id);
                _servers.Remove(id);
            }

            foreach (var server in servers)
            {
                var wasEnabled = _servers.TryGetValue(server.Id, out var existing) && existing.IsEnabled;
                _servers[server.Id] = server;

                if (server.IsEnabled && !wasEnabled)
                {
                    StartPinging(server);
                }
                else if (!server.IsEnabled && wasEnabled)
                {
                    StopPinging(server.Id);
                }
            }
        }
    }

    private void StartPinging(Server server)
    {
        var timer = new System.Threading.Timer(
            async _ => await PingServerAsync(server.Id),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(5)
        );

        _timers[server.Id] = timer;
    }

    private void StopPinging(string serverId)
    {
        if (_timers.TryGetValue(serverId, out var timer))
        {
            timer.Dispose();
            _timers.Remove(serverId);
        }
    }

    private async Task PingServerAsync(string serverId)
    {
        Server? server;
        lock (_lock)
        {
            if (!_servers.TryGetValue(serverId, out server))
                return;
        }

        try
        {
            using var pinger = new Ping();
            var reply = await pinger.SendPingAsync(server.Host, 3000);

            lock (_lock)
            {
                if (!_servers.TryGetValue(serverId, out server))
                    return;

                var previousStatus = server.Status;
                server.LastPingTime = DateTime.Now;

                if (reply.Status == IPStatus.Success)
                {
                    server.Status = ServerStatus.Online;
                    server.ConsecutiveFailures = 0;
                }
                else
                {
                    server.ConsecutiveFailures++;

                    if (server.ConsecutiveFailures >= 3)
                    {
                        server.Status = ServerStatus.Offline;
                    }
                }

                if (previousStatus != server.Status)
                {
                    StatusChanged?.Invoke(this, new ServerStatusChangedEventArgs
                    {
                        Server = server,
                        PreviousStatus = previousStatus
                    });
                }
            }
        }
        catch
        {
            lock (_lock)
            {
                if (!_servers.TryGetValue(serverId, out server))
                    return;

                server.ConsecutiveFailures++;
                server.LastPingTime = DateTime.Now;

                var previousStatus = server.Status;

                if (server.ConsecutiveFailures >= 3)
                {
                    server.Status = ServerStatus.Offline;

                    if (previousStatus != server.Status)
                    {
                        StatusChanged?.Invoke(this, new ServerStatusChangedEventArgs
                        {
                            Server = server,
                            PreviousStatus = previousStatus
                        });
                    }
                }
            }
        }
    }

    public List<Server> GetServers()
    {
        lock (_lock)
        {
            return _servers.Values.ToList();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }
            _timers.Clear();
        }
    }
}

public class ServerStatusChangedEventArgs : EventArgs
{
    public required Server Server { get; init; }
    public ServerStatus PreviousStatus { get; init; }
}
