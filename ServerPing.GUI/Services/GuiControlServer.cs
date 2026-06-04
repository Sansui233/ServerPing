using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace ServerPing.GUI.Services;

public class GuiControlServer : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    public event Action<int, int>? ToggleRequested;

    public void Start()
    {
        Task.Run(ListenLoop);
    }

    private async Task ListenLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    "ServerPing.GuiControl",
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(_cts.Token);

                var buffer = new byte[4096];
                var bytesRead = await server.ReadAsync(buffer, _cts.Token);
                if (bytesRead > 0)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var msg = JsonSerializer.Deserialize<ControlMessage>(json);
                    if (msg?.Command == "Toggle")
                    {
                        ToggleRequested?.Invoke(msg.CursorX, msg.CursorY);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (_cts.IsCancellationRequested) break;
                await Task.Delay(500);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private class ControlMessage
    {
        public string Command { get; set; } = "";
        public int CursorX { get; set; }
        public int CursorY { get; set; }
    }
}
