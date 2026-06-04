using Microsoft.Toolkit.Uwp.Notifications;
using ServerPing.Shared.Models;
using System.Media;
using System.Runtime.InteropServices;
using Windows.UI.Notifications;

namespace ServerPing.Service;

public class NotificationService
{
    private const uint SndAsync = 0x0001;
    private const uint SndAlias = 0x00010000;
    private const uint SndNoDefault = 0x0002;
    private const string NotificationSoundAlias = "SystemNotification";

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PlaySound(string? pszSound, nint hmod, uint fdwSound);

    public void ShowServerOfflineNotification(Server server, int failureThreshold)
    {
        var content = new ToastContentBuilder()
            .AddText("服务器离线")
            .AddText($"{server.Name} ({server.Host})")
            .AddText($"连续 {failureThreshold} 次 Ping 失败，最后检查时间：{server.LastPingTime:HH:mm:ss}")
            .GetToastContent();

        var toast = new ToastNotification(content.GetXml());
        ToastNotificationManager.CreateToastNotifier("ServerPing").Show(toast);
        PlayNotificationSound();
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

    public void ShowTestNotification()
    {
        var content = new ToastContentBuilder()
            .AddText("ServerPing 测试通知")
            .AddText("Windows 通知功能正常")
            .AddText($"发送时间：{DateTime.Now:HH:mm:ss}")
            .GetToastContent();

        var toast = new ToastNotification(content.GetXml());
        ToastNotificationManager.CreateToastNotifier("ServerPing").Show(toast);
    }

    public void PlayNotificationSound()
    {
        if (PlaySound(NotificationSoundAlias, 0, SndAlias | SndAsync | SndNoDefault))
            return;

        SystemSounds.Exclamation.Play();
    }
}
