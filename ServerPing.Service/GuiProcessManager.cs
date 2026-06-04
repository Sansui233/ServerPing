using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace ServerPing.Service;

public class GuiProcessManager
{
    private const string GuiExecutableName = "ServerPing.GUI.exe";

    public void LaunchGui(string? extraArgs = null)
    {
        if (IsGuiRunning())
        {
            Console.WriteLine("GUI 管理面板已在运行");
            return;
        }

        var guiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GuiExecutableName);

        if (!File.Exists(guiPath))
        {
            Console.WriteLine($"GUI 程序未找到: {guiPath}");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = guiPath,
                UseShellExecute = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            };

            if (extraArgs != null)
                psi.Arguments = extraArgs;

            Process.Start(psi);
            Console.WriteLine("已启动 GUI 管理面板");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"启动 GUI 失败: {ex.Message}");
        }
    }

    public async Task ToggleGui(int cursorX, int cursorY)
    {
        if (!IsGuiRunning())
        {
            LaunchGui($"--x {cursorX} --y {cursorY}");
            return;
        }

        try
        {
            using var pipe = new NamedPipeClientStream(".", "ServerPing.GuiControl", PipeDirection.InOut);
            await pipe.ConnectAsync(2000);
            var json = JsonSerializer.Serialize(new { Command = "Toggle", CursorX = cursorX, CursorY = cursorY });
            var bytes = Encoding.UTF8.GetBytes(json);
            await pipe.WriteAsync(bytes);
            await pipe.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Toggle GUI 失败: {ex.Message}");
        }
    }

    public bool IsGuiRunning()
    {
        var processName = Path.GetFileNameWithoutExtension(GuiExecutableName);
        return Process.GetProcessesByName(processName).Length > 0;
    }

    public void CloseGuiIfRunning()
    {
        var processName = Path.GetFileNameWithoutExtension(GuiExecutableName);
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                if (process.CloseMainWindow() && process.WaitForExit(3000))
                {
                    continue;
                }

                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"关闭 GUI 失败: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
