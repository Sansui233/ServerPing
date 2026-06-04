using ServerPing.Shared.Models;

namespace ServerPing.Shared.IPC;

public class IpcMessage
{
    public required MessageType Type { get; set; }
    public object? Data { get; set; }
}

public class IpcResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public object? Data { get; set; }
}

public class AddServerRequest
{
    public required string Name { get; set; }
    public required string Host { get; set; }
}

public class RemoveServerRequest
{
    public required string ServerId { get; set; }
}

public class UpdateSettingsRequest
{
    public required MonitoringSettings Settings { get; set; }
}
