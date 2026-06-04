using System.Text.Json;
using ServerPing.Shared.Models;

namespace ServerPing.Service;

public class StatsFileManager : IDisposable
{
    private static readonly string StatsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServerPing", "stats");

    private readonly Dictionary<string, HourAggregate> _currentHour = new();
    private readonly Dictionary<string, HourAggregate> _previousHour = new();
    private readonly object _lock = new();
    private string _currentHourKey;
    private System.Threading.Timer? _hourlyTimer;

    public StatsFileManager()
    {
        _currentHourKey = GetHourKey(DateTime.Now);
        ScheduleHourlyTimer();
    }

    public void LoadOnStartup(List<string> serverIds)
    {
        lock (_lock)
        {
            foreach (var serverId in serverIds)
            {
                var data = ReadFile(serverId);
                if (data == null || data.Count == 0)
                    continue;

                if (data.TryGetValue(_currentHourKey, out var current))
                {
                    _currentHour[serverId] = current;
                }

                var previousKey = GetHourKey(DateTime.Now.AddHours(-1));
                if (data.TryGetValue(previousKey, out var previous))
                {
                    _previousHour[serverId] = previous;
                }
            }
        }
    }

    public void RecordResult(string serverId, bool success)
    {
        lock (_lock)
        {
            var newKey = GetHourKey(DateTime.Now);
            if (newKey != _currentHourKey)
                FlushAndRotate();

            if (!_currentHour.TryGetValue(serverId, out var agg))
            {
                agg = new HourAggregate();
                _currentHour[serverId] = agg;
            }

            if (success)
                agg.Success++;
            else
                agg.Failure++;
        }
    }

    public PingStatsWindow GetLastHourStats(string serverId)
    {
        lock (_lock)
        {
            var success = 0;
            var failure = 0;

            if (_previousHour.TryGetValue(serverId, out var prev))
            {
                success += prev.Success;
                failure += prev.Failure;
            }

            if (_currentHour.TryGetValue(serverId, out var curr))
            {
                success += curr.Success;
                failure += curr.Failure;
            }

            return new PingStatsWindow { SuccessCount = success, FailureCount = failure };
        }
    }

    public void FlushAll()
    {
        lock (_lock)
        {
            WriteAllServers(_currentHourKey, snapshot: true);
        }
    }

    public void RemoveServer(string serverId)
    {
        lock (_lock)
        {
            _currentHour.Remove(serverId);
            _previousHour.Remove(serverId);
        }
    }

    private void FlushAndRotate()
    {
        WriteAllServers(_currentHourKey, snapshot: false);

        _previousHour.Clear();
        foreach (var (id, agg) in _currentHour)
            _previousHour[id] = agg;

        _currentHour.Clear();
        _currentHourKey = GetHourKey(DateTime.Now);
    }

    private void WriteAllServers(string hourKey, bool snapshot)
    {
        foreach (var (serverId, agg) in _currentHour)
        {
            if (agg.Success == 0 && agg.Failure == 0)
                continue;

            var data = ReadFile(serverId) ?? new Dictionary<string, HourAggregate>();
            data[hourKey] = snapshot ? agg.Clone() : agg;
            PruneOldEntries(data);
            WriteFile(serverId, data);
        }
    }

    private static void PruneOldEntries(Dictionary<string, HourAggregate> data)
    {
        var cutoff = DateTime.Now.AddDays(-7);
        var keysToRemove = data.Keys
            .Where(k => TryParseHourKey(k, out var dt) && dt < cutoff)
            .ToList();
        foreach (var k in keysToRemove)
            data.Remove(k);
    }

    private static Dictionary<string, HourAggregate>? ReadFile(string serverId)
    {
        try
        {
            var path = GetFilePath(serverId);
            if (!File.Exists(path))
                return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, HourAggregate>>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void WriteFile(string serverId, Dictionary<string, HourAggregate> data)
    {
        try
        {
            Directory.CreateDirectory(StatsDirectory);
            var path = GetFilePath(serverId);
            var tmpPath = path + ".tmp";
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"写入统计文件失败 [{serverId}]: {ex.Message}");
        }
    }

    private static string GetFilePath(string serverId) =>
        Path.Combine(StatsDirectory, $"{serverId}.json");

    private static string GetHourKey(DateTime dt)
    {
        var startHour = dt.Hour;
        var endHour = (startHour + 1) % 24;
        return $"{dt:yyyy-MM-dd}-{startHour:D2}-{endHour:D2}";
    }

    private static bool TryParseHourKey(string key, out DateTime result)
    {
        result = default;
        var parts = key.Split('-');
        if (parts.Length < 4)
            return false;
        return DateTime.TryParse($"{parts[0]}-{parts[1]}-{parts[2]}", out result);
    }

    private void ScheduleHourlyTimer()
    {
        var now = DateTime.Now;
        var nextHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);
        var delay = nextHour - now;

        _hourlyTimer = new System.Threading.Timer(_ =>
        {
            lock (_lock)
            {
                FlushAndRotate();
            }
            ScheduleHourlyTimer();
        }, null, delay, Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _hourlyTimer?.Dispose();
    }
}

public class HourAggregate
{
    public int Success { get; set; }
    public int Failure { get; set; }

    public HourAggregate Clone() => new() { Success = Success, Failure = Failure };
}
