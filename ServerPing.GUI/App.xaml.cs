using System.Threading;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using ServerPing.GUI.Services;

namespace ServerPing.GUI;

public partial class App : Application
{
    private static Mutex? _mutex;
    private GuiControlServer? _controlServer;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "ServerPing.GUI.SingleInstance", out var isNew);
        if (!isNew)
        {
            MessageBox.Show("ServerPing 管理面板已在运行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        ApplyTheme(IsSystemDarkMode());
        base.OnStartup(e);

        var mainWindow = new MainWindow();

        _controlServer = new GuiControlServer();
        _controlServer.ToggleRequested += (cursorX, cursorY) =>
        {
            Dispatcher.Invoke(() => mainWindow.HandleToggle(cursorX, cursorY));
        };
        _controlServer.Start();

        MainWindow = mainWindow;

        var (posX, posY) = ParsePositionArgs(e.Args);
        if (posX.HasValue && posY.HasValue)
        {
            mainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            mainWindow.Show();
            mainWindow.PositionNearCursor(posX.Value, posY.Value);
        }
        else
        {
            mainWindow.Show();
        }

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _controlServer?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static (int? X, int? Y) ParsePositionArgs(string[] args)
    {
        int? x = null, y = null;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--x" && int.TryParse(args[i + 1], out var px))
                x = px;
            if (args[i] == "--y" && int.TryParse(args[i + 1], out var py))
                y = py;
        }
        return (x, y);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Dispatcher.Invoke(() => ApplyTheme(IsSystemDarkMode()));
    }

    private void ApplyTheme(bool isDark)
    {
        if (isDark)
        {
            Set("AppBackgroundBrush",        0x11, 0x13, 0x17);
            Set("TitleBarBrush",             0x11, 0x13, 0x17);
            Set("PanelBackgroundBrush",      0x19, 0x1D, 0x23);
            Set("PanelAltBackgroundBrush",   0x20, 0x25, 0x2D);
            Set("BorderBrushDark",           0x30, 0x37, 0x41);
            Set("TextBrush",                 0xE6, 0xEA, 0xF0);
            Set("MutedTextBrush",            0x98, 0xA2, 0xB3);
            Set("ObscuredTextBrush",         0x66, 0x70, 0x85);
            Set("AccentBrush",               0x4F, 0x8C, 0xFF);
            Set("DangerBrush",               0xF8, 0x71, 0x71);
            Set("ButtonBackgroundBrush",     0x25, 0x2B, 0x34);
            Set("ButtonHoverBrush",          0x30, 0x38, 0x46);
            Set("TextBoxBackgroundBrush",    0x14, 0x18, 0x20);
            SetA("TitleBarHoverBrush",   0x1E, 0xFF, 0xFF, 0xFF);
            Set("DataGridRowAltBrush",       0x17, 0x1B, 0x21);
            Set("DataGridGridLineBrush",     0x25, 0x2A, 0x32);
            Set("DataGridHeaderBrush",       0x20, 0x25, 0x2D);
            Set("CheckBoxBoxBrush",          0x14, 0x18, 0x20);
            SetA("DataGridCellSelectedBrush", 0x1E, 0x4F, 0x8C, 0xFF);
            Set("ToolTipBackgroundBrush",    0x2A, 0x2E, 0x36);
            Set("ToolTipForegroundBrush",    0xE6, 0xEA, 0xF0);
            Set("ToolTipBorderBrush",        0x40, 0x48, 0x58);
        }
        else
        {
            Set("AppBackgroundBrush",        0xFF, 0xFF, 0xFF);
            Set("TitleBarBrush",             0xF8, 0xF9, 0xFA);
            Set("PanelBackgroundBrush",      0xF3, 0xF4, 0xF6);
            Set("PanelAltBackgroundBrush",   0xE5, 0xE7, 0xEB);
            Set("BorderBrushDark",           0xD1, 0xD5, 0xDB);
            Set("TextBrush",                 0x1A, 0x1A, 0x1A);
            Set("MutedTextBrush",            0x6B, 0x72, 0x80);
            Set("ObscuredTextBrush",         0x9C, 0xA3, 0xAF);
            Set("AccentBrush",               0x4F, 0x8C, 0xFF);
            Set("DangerBrush",               0xEF, 0x44, 0x44);
            Set("ButtonBackgroundBrush",     0xE5, 0xE7, 0xEB);
            Set("ButtonHoverBrush",          0xD1, 0xD5, 0xDB);
            Set("TextBoxBackgroundBrush",    0xFF, 0xFF, 0xFF);
            SetA("TitleBarHoverBrush",   0x0D, 0x00, 0x00, 0x00);
            Set("DataGridRowAltBrush",       0xF9, 0xFA, 0xFB);
            Set("DataGridGridLineBrush",     0xE5, 0xE7, 0xEB);
            Set("DataGridHeaderBrush",       0xF3, 0xF4, 0xF6);
            Set("CheckBoxBoxBrush",          0xFF, 0xFF, 0xFF);
            SetA("DataGridCellSelectedBrush", 0x1E, 0x4F, 0x8C, 0xFF);
            Set("ToolTipBackgroundBrush",    0xF9, 0xFA, 0xFB);
            Set("ToolTipForegroundBrush",    0x1A, 0x1A, 0x1A);
            Set("ToolTipBorderBrush",        0xD0, 0xD5, 0xDD);
        }
    }

    private void Set(string key, byte r, byte g, byte b) =>
        Resources[key] = new SolidColorBrush(Color.FromRgb(r, g, b));

    private void SetA(string key, byte a, byte r, byte g, byte b) =>
        Resources[key] = new SolidColorBrush(Color.FromArgb(a, r, g, b));

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int i && i == 0;
        }
        catch
        {
            return true;
        }
    }
}
