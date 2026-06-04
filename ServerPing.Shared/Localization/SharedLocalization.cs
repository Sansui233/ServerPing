using System.Globalization;
using ServerPing.Shared.Models;

namespace ServerPing.Shared.Localization;

public static class SharedLocalization
{
    public const string SystemLanguage = "system";
    public const string Chinese = "zh-CN";
    public const string English = "en-US";
    public const string Russian = "ru-RU";

    private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
    {
        [Chinese] = new()
        {
            ["Tray.OpenGui"] = "打开管理面板",
            ["Tray.PauseMonitoring"] = "暂停监控",
            ["Tray.ResumeMonitoring"] = "恢复监控",
            ["Tray.LaunchAtStartup"] = "开机自启",
            ["Tray.Exit"] = "退出",
            ["Tray.NoServers"] = "暂无服务器",
            ["Tray.Online"] = "在线",
            ["Tray.Offline"] = "离线",
            ["Tray.Unknown"] = "未知",
            ["Tray.Availability"] = "可用率",
            ["Tray.Tooltip"] = "ServerPing - {0}/{1} 在线 - 可用率 {2}"
        },
        [English] = new()
        {
            ["Tray.OpenGui"] = "Open management panel",
            ["Tray.PauseMonitoring"] = "Pause monitoring",
            ["Tray.ResumeMonitoring"] = "Resume monitoring",
            ["Tray.LaunchAtStartup"] = "Launch at startup",
            ["Tray.Exit"] = "Exit",
            ["Tray.NoServers"] = "No servers",
            ["Tray.Online"] = "Online",
            ["Tray.Offline"] = "Offline",
            ["Tray.Unknown"] = "Unknown",
            ["Tray.Availability"] = "Availability",
            ["Tray.Tooltip"] = "ServerPing - {0}/{1} online - availability {2}"
        },
        [Russian] = new()
        {
            ["Tray.OpenGui"] = "Открыть панель управления",
            ["Tray.PauseMonitoring"] = "Приостановить мониторинг",
            ["Tray.ResumeMonitoring"] = "Возобновить мониторинг",
            ["Tray.LaunchAtStartup"] = "Запускать при входе в Windows",
            ["Tray.Exit"] = "Выход",
            ["Tray.NoServers"] = "Нет серверов",
            ["Tray.Online"] = "Онлайн",
            ["Tray.Offline"] = "Офлайн",
            ["Tray.Unknown"] = "Неизвестно",
            ["Tray.Availability"] = "Доступность",
            ["Tray.Tooltip"] = "ServerPing - {0}/{1} онлайн - доступность {2}"
        }
    };

    public static string Normalize(string? language) =>
        language is SystemLanguage or Chinese or English or Russian ? language : SystemLanguage;

    public static string Resolve(string? language)
    {
        var normalized = Normalize(language);
        if (normalized != SystemLanguage)
            return normalized;

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "zh" => Chinese,
            "ru" => Russian,
            _ => English
        };
    }

    public static string Get(string language, string key)
    {
        var resolved = Resolve(language);
        return Strings.TryGetValue(resolved, out var dictionary) && dictionary.TryGetValue(key, out var value)
            ? value
            : key;
    }

    public static string StatusText(string language, ServerStatus status) => status switch
    {
        ServerStatus.Online => Get(language, "Tray.Online"),
        ServerStatus.Offline => Get(language, "Tray.Offline"),
        _ => Get(language, "Tray.Unknown")
    };
}
