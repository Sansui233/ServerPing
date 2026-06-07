using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ServerPing.Shared.IPC;
using ServerPing.Shared.Models;

namespace ServerPing.GUI.Services;

public class IpcClient
{
    private const string PipeName = "ServerPing";

    public async Task<List<Server>> GetServersAsync()
    {
        var response = await SendMessageAsync(new IpcMessage
        {
            Type = MessageType.GetServers
        });

        if (response.Success && response.Data != null)
        {
            var json = JsonSerializer.Serialize(response.Data);
            return JsonSerializer.Deserialize<List<Server>>(json) ?? [];
        }

        return [];
    }

    public async Task<Server?> AddServerAsync(string name, string host)
    {
        var response = await SendMessageAsync(new IpcMessage
        {
            Type = MessageType.AddServer,
            Data = new AddServerRequest { Name = name, Host = host }
        });

        if (response.Success && response.Data != null)
        {
            var json = JsonSerializer.Serialize(response.Data);
            return JsonSerializer.Deserialize<Server>(json);
        }

        return null;
    }

    public async Task<bool> RemoveServerAsync(string serverId)
    {
        var response = await SendMessageAsync(new IpcMessage
        {
            Type = MessageType.RemoveServer,
            Data = new RemoveServerRequest { ServerId = serverId }
        });

        return response.Success;
    }

    public async Task<bool> UpdateServersAsync(List<Server> servers)
    {
        var response = await SendMessageAsync(new IpcMessage
        {
            Type = MessageType.UpdateServers,
            Data = servers
        });

        return response.Success;
    }

    public async Task<MonitoringSettings> GetSettingsAsync()
    {
        var response = await SendMessageAsync(new IpcMessage
        {
            Type = MessageType.GetSettings
        });

        if (response.Success && response.Data != null)
        {
            var json = JsonSerializer.Serialize(response.Data);
            return JsonSerializer.Deserialize<MonitoringSettings>(json) ?? new MonitoringSettings();
        }

        return new MonitoringSettings();
    }

    public async Task<MonitoringSettings?> UpdateSettingsAsync(MonitoringSettings settings)
    {
        var response = await SendMessageAsync(new IpcMessage
        {
            Type = MessageType.UpdateSettings,
            Data = new UpdateSettingsRequest { Settings = settings }
        });

        if (response.Success && response.Data != null)
        {
            var json = JsonSerializer.Serialize(response.Data);
            return JsonSerializer.Deserialize<MonitoringSettings>(json);
        }

        return null;
    }

    public async Task<bool> TestNotificationAsync()
    {
        var response = await SendMessageAsync(new IpcMessage
        {
            Type = MessageType.TestNotification
        });

        return response.Success;
    }

    public async Task<bool> TestNotificationSoundAsync()
    {
        var response = await SendMessageAsync(new IpcMessage
        {
            Type = MessageType.TestNotificationSound
        });

        return response.Success;
    }

    public async Task<List<ServerStats>> GetServerStatsAsync()
    {
        var response = await SendMessageAsync(new IpcMessage
        {
            Type = MessageType.GetServerStats
        });

        if (response.Success && response.Data != null)
        {
            var json = JsonSerializer.Serialize(response.Data);
            return JsonSerializer.Deserialize<List<ServerStats>>(json) ?? [];
        }

        return [];
    }

    public async Task<ServiceStatus?> GetStatusAsync()
    {
        var response = await SendMessageAsync(new IpcMessage
        {
            Type = MessageType.GetStatus
        });

        if (response.Success && response.Data != null)
        {
            var json = JsonSerializer.Serialize(response.Data);
            return JsonSerializer.Deserialize<ServiceStatus>(json);
        }

        return null;
    }

    private async Task<IpcResponse> SendMessageAsync(IpcMessage message)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(3000);

            var messageJson = JsonSerializer.Serialize(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            await pipe.WriteAsync(messageBytes);
            await pipe.FlushAsync();

            var buffer = new byte[65536];
            var responseBuilder = new StringBuilder();

            int bytesRead;
            while ((bytesRead = await pipe.ReadAsync(buffer)) > 0)
            {
                responseBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                if (bytesRead < buffer.Length)
                    break;
            }

            return JsonSerializer.Deserialize<IpcResponse>(responseBuilder.ToString())
                   ?? new IpcResponse { Success = false, ErrorMessage = LocalizationService.Get("Message.DeserializeFailed") };
        }
        catch (TimeoutException)
        {
            return new IpcResponse { Success = false, ErrorMessage = LocalizationService.Get("Message.ServiceConnectionTimeout") };
        }
        catch (Exception ex)
        {
            return new IpcResponse { Success = false, ErrorMessage = LocalizationService.Format("Message.CommunicationFailed", ex.Message) };
        }
    }
}
