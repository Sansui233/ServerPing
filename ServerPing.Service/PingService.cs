using System.Net.NetworkInformation;
using ServerPing.Shared.Models;

namespace ServerPing.Service;

public class PingService : IDisposable
{
    private readonly Dictionary<string, System.Threading.Timer> _timers = new();
    private readonly Dictionary<string, Server> _servers = new();
    private readonly Dictionary<string, List<PingSample>> _history = new();
    private readonly object _lock = new();
    private MonitoringSettings _settings = new();
    private bool _isPaused;

    public event EventHandler<ServerStatusChangedEventArgs>? StatusChanged;

    public void Start(List<Server> servers, MonitoringSettings? settings = null)
    {
        lock (_lock)
        {
            if (settings != null)
            {
                _settings = settings.Clone();
            }

            foreach (var server in servers)
            {
                _servers[server.Id] = server;
                _history.TryAdd(server.Id, []);

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
                _history.Remove(id);
            }

            foreach (var server in servers)
            {
                if (_servers.TryGetValue(server.Id, out var existing))
                {
                    var wasEnabled = existing.IsEnabled;
                    // preserve runtime state — only update user-editable fields
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
                    _history.TryAdd(server.Id, []);

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
        var now = DateTime.Now;
        if (!_history.TryGetValue(serverId, out var samples))
        {
            samples = [];
            _history[serverId] = samples;
        }

        samples.Add(new PingSample(now, wasSuccessful));
        PruneHistory(samples, now);
    }

    private static void PruneHistory(List<PingSample> samples, DateTime now)
    {
        var cutoff = now.AddHours(-24);
        samples.RemoveAll(s => s.Timestamp < cutoff);
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
            var now = DateTime.Now;

            foreach (var samples in _history.Values)
            {
                PruneHistory(samples, now);
            }

            return _servers.Keys.Select(id => new ServerStats
            {
                ServerId = id,
                LastHour = BuildStatsWindow(_history.GetValueOrDefault(id), now.AddHours(-1)),
                LastDay = BuildStatsWindow(_history.GetValueOrDefault(id), now.AddDays(-1))
            }).ToList();
        }
    }

    private static PingStatsWindow BuildStatsWindow(List<PingSample>? samples, DateTime cutoff)
    {
        if (samples == null)
            return new PingStatsWindow();

        return new PingStatsWindow
        {
            SuccessCount = samples.Count(s => s.Timestamp >= cutoff && s.WasSuccessful),
            FailureCount = samples.Count(s => s.Timestamp >= cutoff && !s.WasSuccessful)
        };
    }

    public double? GetLastHourAvailability(string serverId)
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            var stats = BuildStatsWindow(_history.GetValueOrDefault(serverId), now.AddHours(-1));
            return stats.AvailabilityPercent;
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

public readonly record struct PingSample(DateTime Timestamp, bool WasSuccessful);

public class ServerStatusChangedEventArgs : EventArgs
{
    public required Server Server { get; init; }
    public ServerStatus PreviousStatus { get; init; }
}
