using ServerPing.Shared.Models;

namespace ServerPing.Service;

public enum PingResult : byte
{
    Empty = 0,
    Success = 1,
    Failure = 2
}

public class MinuteRingBuffer
{
    private const int Rows = 10;
    private const int Cols = 60;

    private readonly PingResult[,] _data = new PingResult[Rows, Cols];
    private readonly long[] _minuteKeys = new long[Rows];
    private readonly long _epochTicks;
    private readonly object _lock = new();
    private int _currentRow;
    private int _currentSlot;

    public MinuteRingBuffer()
    {
        _epochTicks = DateTime.UtcNow.Ticks;
        _minuteKeys[0] = 0;
    }

    public void Record(bool success)
    {
        lock (_lock)
        {
            var currentKey = GetCurrentMinuteKey();
            AdvanceToMinute(currentKey);

            if (_currentSlot < Cols)
            {
                _data[_currentRow, _currentSlot] = success ? PingResult.Success : PingResult.Failure;
                _currentSlot++;
            }
        }
    }

    public MinuteStats[] GetRecentMinutes()
    {
        lock (_lock)
        {
            AdvanceToMinute(GetCurrentMinuteKey());

            var result = new MinuteStats[Rows];
            for (var i = 0; i < Rows; i++)
            {
                var row = (_currentRow + 1 + i) % Rows;
                int success = 0, failure = 0;
                for (var col = 0; col < Cols; col++)
                {
                    switch (_data[row, col])
                    {
                        case PingResult.Success: success++; break;
                        case PingResult.Failure: failure++; break;
                    }
                }
                result[i] = new MinuteStats { SuccessCount = success, FailureCount = failure };
            }
            return result;
        }
    }

    private long GetCurrentMinuteKey()
    {
        return (DateTime.UtcNow.Ticks - _epochTicks) / TimeSpan.TicksPerMinute;
    }

    private void AdvanceToMinute(long targetKey)
    {
        var storedKey = _minuteKeys[_currentRow];
        if (targetKey <= storedKey)
            return;

        var gap = targetKey - storedKey;
        var steps = (int)Math.Min(gap, Rows);

        for (var s = 0; s < steps; s++)
        {
            _currentRow = (_currentRow + 1) % Rows;
            ClearRow(_currentRow);
            _minuteKeys[_currentRow] = storedKey + (gap - steps) + s + 1;
        }
        _currentSlot = 0;
    }

    private void ClearRow(int row)
    {
        for (var col = 0; col < Cols; col++)
            _data[row, col] = PingResult.Empty;
    }
}
