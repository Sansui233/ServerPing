using System.Net.NetworkInformation;
using ServerPing.Shared.Models;

namespace ServerPing.Service;

public class PingService : IDisposable
{
    private readonly Dictionary<string, System.Threading.Timer> _timers = new();
    private readonly Dictionary<string, Server> _servers = new();
    private readonly Dictionary<string, MinuteRingBuffer> _minuteBuffers = new();
    private readonly StatsFileManager _statsFileManager;
    private readonly object _lock = new();
    private MonitoringSettings _settings = new();
    private bool _isPaused;

    public event EventHandler<ServerStatusChangedEventArgs>? StatusChanged;
    public event EventHandler? SettingsChanged;

    public PingService(StatsFileManager statsFileManager)
    {
        _statsFileManager = statsFileManager;
    }

    public void Start(List<Server> servers, MonitoringSettings? settings = null)
    {
        lock (_lock)
        {
            if (settings != null)
            {
                _settings = settings.Clone();
            }

            var serverIds = new List<string>();
            foreach (var server in servers)
            {
                _servers[server.Id] = server;
                _minuteBuffers.TryAdd(server.Id, new MinuteRingBuffer());
                serverIds.Add(server.Id);

                if (server.IsEnabled)
                {
                    StartPinging(server);
                }
            }

            _statsFileManager.LoadOnStartup(serverIds);
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
                _minuteBuffers.Remove(id);
                _statsFileManager.RemoveServer(id);
            }

            foreach (var server in servers)
            {
                if (_servers.TryGetValue(server.Id, out var existing))
                {
                    var wasEnabled = existing.IsEnabled;
                    existing.Name = server.Name;
                    existing.Host = server.Host;
                    existing.IsEnabled = server.IsEnabled;

                    if (server.IsEnabled && !wasEnabled)
                        StartPinging(existing);
                    else if (!server.IsEnabled && wasEnabled)
                        StopPinging(server.Id);
                }
                else
                {
                    _servers[server.Id] = server;
                    _minuteBuffers.TryAdd(server.Id, new MinuteRingBuffer());

                    if (server.IsEnabled)
                        StartPinging(server);
                }
            }
        }
    }

    private void StartPinging(Server server)
    {
        StopPinging(server.Id);

        var timer = new System.Threading.Timer(
            async _ => await PingServerAsync(server.Id),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(_settings.PingIntervalSeconds)
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

            if (server.Host is "0.0.0.0" or "127.0.0.1" or "localhost")
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
                    RecordPingResult(serverId, true);
                }
                else
                {
                    server.ConsecutiveFailures++;
                    RecordPingResult(serverId, false);

                    if (server.ConsecutiveFailures >= _settings.FailureThreshold)
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
                RecordPingResult(serverId, false);

                var previousStatus = server.Status;

                if (server.ConsecutiveFailures >= _settings.FailureThreshold)
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

    private void RecordPingResult(string serverId, bool wasSuccessful)
    {
        _statsFileManager.RecordResult(serverId, wasSuccessful);
        if (_minuteBuffers.TryGetValue(serverId, out var buffer))
            buffer.Record(wasSuccessful);
    }

    public List<Server> GetServers()
    {
        lock (_lock)
        {
            return _servers.Values.ToList();
        }
    }

    public MonitoringSettings GetSettings()
    {
        lock (_lock)
        {
            return _settings.Clone();
        }
    }

    public void UpdateSettings(MonitoringSettings settings)
    {
        lock (_lock)
        {
            _settings = settings.Clone();

            if (!_isPaused)
            {
                foreach (var server in _servers.Values.Where(s => s.IsEnabled))
                {
                    StartPinging(server);
                }
            }
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_isPaused) return;
            _isPaused = true;
            foreach (var id in _timers.Keys.ToList())
                StopPinging(id);
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (!_isPaused) return;
            _isPaused = false;
            foreach (var server in _servers.Values.Where(s => s.IsEnabled))
                StartPinging(server);
        }
    }

    public List<ServerStats> GetStats()
    {
        lock (_lock)
        {
            return _servers.Keys.Select(id => new ServerStats
            {
                ServerId = id,
                LastHour = _statsFileManager.GetLastHourStats(id),
                RecentMinutes = _minuteBuffers.TryGetValue(id, out var buf)
                    ? buf.GetRecentMinutes()
                    : []
            }).ToList();
        }
    }

    public double? GetLastHourAvailability(string serverId)
    {
        return _statsFileManager.GetLastHourStats(serverId).AvailabilityPercent;
    }

    public void Dispose()
    {
        _statsFileManager.FlushAll();

        lock (_lock)
        {
            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }
            _timers.Clear();
        }

        _statsFileManager.Dispose();
    }
}

public class ServerStatusChangedEventArgs : EventArgs
{
    public required Server Server { get; init; }
    public ServerStatus PreviousStatus { get; init; }
}
