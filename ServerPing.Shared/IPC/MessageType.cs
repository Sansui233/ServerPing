namespace ServerPing.Shared.IPC;

public enum MessageType
{
    GetServers,
    UpdateServers,
    AddServer,
    RemoveServer,
    GetStatus,
    GetSettings,
    UpdateSettings,
    TestNotification,
    TestNotificationSound,
    GetServerStats,
    Response
}
