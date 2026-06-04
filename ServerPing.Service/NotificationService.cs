using Microsoft.Toolkit.Uwp.Notifications;
using ServerPing.Shared.Models;
using Windows.UI.Notifications;

namespace ServerPing.Service;

public class NotificationService
{
    public void ShowServerOfflineNotification(Server server)
    {
        var content = new ToastContentBuilder()
            .AddText("服务器离线")
            .AddText($"{server.Name} ({server.Host})")
            .AddText($"连续 3 次 Ping 失败，最后检查时间：{server.LastPingTime:HH:mm:ss}")
            .GetToastContent();

        var toast = new ToastNotification(content.GetXml());
        ToastNotificationManager.CreateToastNotifier("ServerPing").Show(toast);
    }

    public void ShowServerOnlineNotification(Server server)
    {
        var content = new ToastContentBuilder()
            .AddText("服务器恢复在线")
            .AddText($"{server.Name} ({server.Host})")
            .AddText($"服务器已恢复连接")
            .GetToastContent();

        var toast = new ToastNotification(content.GetXml());
        ToastNotificationManager.CreateToastNotifier("ServerPing").Show(toast);
    }
}
