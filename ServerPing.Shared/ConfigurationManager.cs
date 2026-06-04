using System.Text.Json;
using ServerPing.Shared.Models;

namespace ServerPing.Shared;

public class ConfigurationManager
{
    public static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ServerPing"
    );

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "servers.json");

    public static ServerConfiguration Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                return new ServerConfiguration();
            }

            var json = File.ReadAllText(ConfigFilePath);
            var configuration = JsonSerializer.Deserialize<ServerConfiguration>(json) ?? new ServerConfiguration();
            configuration.Settings ??= new MonitoringSettings();
            configuration.Servers ??= [];
            return configuration;
        }
        catch
        {
            return new ServerConfiguration();
        }
    }

    public static void Save(ServerConfiguration configuration)
    {
        Directory.CreateDirectory(ConfigDirectory);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(configuration, options);
        File.WriteAllText(ConfigFilePath, json);
    }
}
