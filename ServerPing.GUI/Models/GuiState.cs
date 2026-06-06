namespace ServerPing.GUI.Models;

public class GuiState
{
    public ServerSortMode NameSortMode { get; set; } = ServerSortMode.Auto;
    public List<string> ServerOrderIds { get; set; } = [];
}
