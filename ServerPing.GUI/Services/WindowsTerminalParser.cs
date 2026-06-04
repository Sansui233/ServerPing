using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ServerPing.GUI.Services;

public class SshProfile
{
    public required string Name { get; set; }
    public required string Host { get; set; }
    public required string CommandLine { get; set; }
}

public partial class WindowsTerminalParser
{
    public static List<SshProfile> Parse()
    {
        var settingsPath = FindSettingsFile();
        if (settingsPath == null)
            throw new FileNotFoundException("未找到 Windows Terminal 配置文件");

        var json = File.ReadAllText(settingsPath);
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });

        var profiles = new List<SshProfile>();
        if (!doc.RootElement.TryGetProperty("profiles", out var profilesElement))
            return profiles;

        JsonElement listElement;
        if (profilesElement.TryGetProperty("list", out listElement))
        {
        }
        else if (profilesElement.ValueKind == JsonValueKind.Array)
        {
            listElement = profilesElement;
        }
        else
        {
            return profiles;
        }

        foreach (var profile in listElement.EnumerateArray())
        {
            var commandLine = profile.TryGetProperty("commandline", out var cmd)
                ? cmd.GetString()
                : null;

            if (string.IsNullOrEmpty(commandLine) || !commandLine.Contains("ssh", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = profile.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var host = ExtractHostFromSshCommand(commandLine);

            if (!string.IsNullOrEmpty(host))
            {
                profiles.Add(new SshProfile
                {
                    Name = name,
                    Host = host,
                    CommandLine = commandLine
                });
            }
        }

        return profiles;
    }

    private static string? FindSettingsFile()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var packagesDir = Path.Combine(localAppData, "Packages");

        if (!Directory.Exists(packagesDir))
            return null;

        var candidates = Directory.GetDirectories(packagesDir, "Microsoft.WindowsTerminal_*");
        foreach (var dir in candidates)
        {
            var path = Path.Combine(dir, "LocalState", "settings.json");
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    static string? ExtractHostFromSshCommand(string commandLine)
    {
        // ssh [options] user@host / ssh [options] host
        var match = SshHostRegex().Match(commandLine);
        if (match.Success)
            return match.Groups["host"].Value;

        var match2 = SshHostFallbackRegex().Match(commandLine);
        if (match2.Success)
            return match2.Groups["host"].Value;

        return null;
    }

    [GeneratedRegex(@"ssh\s+(?:.*\s+)?(?:\w+@)(?<host>[\w.\-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SshHostRegex();

    [GeneratedRegex(@"ssh\s+(?:.*\s+)?(?<host>[\w.\-]+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex SshHostFallbackRegex();
}
