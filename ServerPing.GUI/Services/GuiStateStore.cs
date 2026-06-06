using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ServerPing.GUI.Models;
using ServerPing.Shared;

namespace ServerPing.GUI.Services;

public class GuiStateStore
{
    private static readonly string StateFilePath = Path.Combine(ConfigurationManager.ConfigDirectory, "gui-state.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public GuiState Load()
    {
        try
        {
            if (!File.Exists(StateFilePath))
                return new GuiState();

            var json = File.ReadAllText(StateFilePath);
            var state = JsonSerializer.Deserialize<GuiState>(json, JsonOptions) ?? new GuiState();
            state.ServerOrderIds ??= [];
            return state;
        }
        catch
        {
            return new GuiState();
        }
    }

    public void Save(GuiState state)
    {
        try
        {
            Directory.CreateDirectory(ConfigurationManager.ConfigDirectory);
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(StateFilePath, json);
        }
        catch
        {
            // GUI state is non-critical; service-backed server config remains the source of truth.
        }
    }
}
