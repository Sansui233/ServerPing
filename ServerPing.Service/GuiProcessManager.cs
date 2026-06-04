using System.Diagnostics;

namespace ServerPing.Service;

public class GuiProcessManager
{
    private const string GuiExecutableName = "ServerPing.GUI.exe";

    public void LaunchGui()
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
            Process.Start(new ProcessStartInfo
            {
                FileName = guiPath,
                UseShellExecute = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            });

            Console.WriteLine("已启动 GUI 管理面板");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"启动 GUI 失败: {ex.Message}");
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
