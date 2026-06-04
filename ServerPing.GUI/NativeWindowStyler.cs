using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ServerPing.GUI;

internal static class NativeWindowStyler
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowCornerRound = 2;

    public static void ApplyWindows11RoundedCorners(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;

        var hwnd = new WindowInteropHelper(window).Handle;
        var preference = DwmWindowCornerRound;
        _ = DwmSetWindowAttribute(
            hwnd,
            DwmWindowCornerPreference,
            ref preference,
            Marshal.SizeOf<int>());
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
