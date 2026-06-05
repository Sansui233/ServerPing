using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ServerPing.Shared;
using ServerPing.Shared.IPC;
using ServerPing.Shared.Models;

namespace ServerPing.Service;

public class IpcServer : IDisposable
{
    private const string PipeName = "ServerPing";
    private readonly CancellationTokenSource _cts = new();
    private readonly PingService _pingService;
    private readonly NotificationService _notificationService;
    private readonly StartupRegistrationService _startupRegistrationService;
    private Task? _serverTask;

    public IpcServer(
        PingService pingService,
        NotificationService notificationService,
        StartupRegistrationService startupRegistrationService)
    {
        _pingService = pingService;
        _notificationService = notificationService;
        _startupRegistrationService = startupRegistrationService;
    }

    public void Start()
    {
        _serverTask = Task.Run(async () => await ServerLoopAsync());
    }

    private async Task ServerLoopAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(_cts.Token);
                await HandleClientAsync(pipe);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"IPC Server 错误: {ex.Message}");
                await Task.Delay(1000, _cts.Token);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe)
    {
        try
        {
            var buffer = new byte[4096];
            var messageBuilder = new StringBuilder();

            int bytesRead;
            while ((bytesRead = await pipe.ReadAsync(buffer, _cts.Token)) > 0)
            {
                messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                if (bytesRead < buffer.Length)
                    break;
            }

            var messageJson = messageBuilder.ToString();
            var message = JsonSerializer.Deserialize<IpcMessage>(messageJson);

            if (message != null)
            {
                var response = HandleMessage(message);
                var responseJson = JsonSerializer.Serialize(response);
                var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                await pipe.WriteAsync(responseBytes, _cts.Token);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理客户端请求失败: {ex.Message}");
        }
    }

    private IpcResponse HandleMessage(IpcMessage message)
    {
        try
        {
            switch (message.Type)
            {
                case MessageType.GetServers:
                    var servers = _pingService.GetServers();
                    return new IpcResponse
                    {
                        Success = true,
                        Data = servers
                    };

                case MessageType.UpdateServers:
                    var serverList = JsonSerializer.Deserialize<List<Server>>(
                        JsonSerializer.Serialize(message.Data));

                    if (serverList != null)
                    {
                        var config = ConfigurationManager.Load();
                        config.Servers = serverList;
                        ConfigurationManager.Save(config);
                        _pingService.UpdateServers(serverList);

                        return new IpcResponse { Success = true };
                    }
                    break;

                case MessageType.AddServer:
                    var addRequest = JsonSerializer.Deserialize<AddServerRequest>(
                        JsonSerializer.Serialize(message.Data));

                    if (addRequest != null)
                    {
                        var config = ConfigurationManager.Load();
                        var newServer = new Server
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = addRequest.Name,
                            Host = addRequest.Host,
                            IsEnabled = true
                        };

                        config.Servers.Add(newServer);
                        ConfigurationManager.Save(config);
                        _pingService.UpdateServers(config.Servers);

                        return new IpcResponse
                        {
                            Success = true,
                            Data = newServer
                        };
                    }
                    break;

                case MessageType.RemoveServer:
                    var removeRequest = JsonSerializer.Deserialize<RemoveServerRequest>(
                        JsonSerializer.Serialize(message.Data));

                    if (removeRequest != null)
                    {
                        var config = ConfigurationManager.Load();
                        config.Servers.RemoveAll(s => s.Id == removeRequest.ServerId);
                        ConfigurationManager.Save(config);
                        _pingService.UpdateServers(config.Servers);

                        return new IpcResponse { Success = true };
                    }
                    break;

                case MessageType.GetStatus:
                    var statusServers = _pingService.GetServers();
                    var onlineCount = statusServers.Count(s => s.Status == ServerStatus.Online);

                    return new IpcResponse
                    {
                        Success = true,
                        Data = new { OnlineCount = onlineCount, TotalCount = statusServers.Count }
                    };

                case MessageType.GetSettings:
                    return new IpcResponse
                    {
                        Success = true,
                        Data = _pingService.GetSettings()
                    };

                case MessageType.UpdateSettings:
                    var settingsRequest = JsonSerializer.Deserialize<UpdateSettingsRequest>(
                        JsonSerializer.Serialize(message.Data));

                    if (settingsRequest != null)
                    {
                        var config = ConfigurationManager.Load();
                        config.Settings = settingsRequest.Settings;
                        _startupRegistrationService.Apply(config.Settings.LaunchAtStartup);
                        ConfigurationManager.Save(config);
                        _pingService.UpdateSettings(config.Settings);

                        return new IpcResponse
                        {
                            Success = true,
                            Data = config.Settings
                        };
                    }
                    break;

                case MessageType.TestNotification:
                    return new IpcResponse { Success = _notificationService.ShowTestNotification() };

                case MessageType.TestNotificationSound:
                    _notificationService.PlayNotificationSound();
                    return new IpcResponse { Success = true };

                case MessageType.GetServerStats:
                    return new IpcResponse
                    {
                        Success = true,
                        Data = _pingService.GetStats()
                    };
            }

            return new IpcResponse
            {
                Success = false,
                ErrorMessage = "未知的消息类型或无效的请求数据"
            };
        }
        catch (Exception ex)
        {
            return new IpcResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _serverTask?.Wait(TimeSpan.FromSeconds(5));
        _cts.Dispose();
    }
}
